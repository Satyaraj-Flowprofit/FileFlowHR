using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Dashboard;

public sealed record CompanyStats(int Employees, int OpenPositions, int VacantPositions, int OpenPostings, int ActiveApplications);
public sealed record MyJob(string PositionCode, string PositionTitle, string BusinessUnit, string? ManagerTitle, string? ManagerName);
public sealed record DashboardDto(string Name, CompanyStats? Stats, MyJob? MyJob, int TeamSize, int OpenInternalJobs, int MyApplications);

[RequiresSignIn]
public sealed record GetDashboardQuery : IQuery<Result<DashboardDto>>;

public sealed class GetDashboardHandler(IAppDbContext db, ICurrentUser user, IPermissionService permissions,
    IAccessScopeResolver scope, TimeProvider clock) : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var today = clock.Today();
        var person = user.PersonId is { } pid ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;

        CompanyStats? stats = null;
        if (permissions.Has(Permissions.DashboardView) && permissions.Reach(Permissions.PeopleView) is { Company: true })
        {
            var openPositions = db.Positions.Where(p => p.EffectiveFrom <= today && (p.EffectiveTo == null || p.EffectiveTo > today));
            var filled = db.PositionAssignments.Where(a => a.AssignmentType == AssignmentType.Primary && a.EffectiveFrom <= today
                && (a.EffectiveTo == null || a.EffectiveTo > today)).Select(a => a.PositionId);
            var closed = new[] { ApplicationStatus.Hired, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn };
            stats = new CompanyStats(
                await db.Employments.CountAsync(e => e.HireDate <= today && (e.TerminationDate == null || e.TerminationDate > today), ct),
                await openPositions.CountAsync(ct),
                await openPositions.CountAsync(p => !filled.Contains(p.Id), ct),
                await db.JobPostings.CountAsync(j => j.Status == PostingStatus.Open, ct),
                await db.JobApplications.CountAsync(a => !closed.Contains(a.Status), ct));
        }

        MyJob? job = null;
        var teamSize = 0;
        if (person is not null)
        {
            job = await (from a in db.PositionAssignments.AsNoTracking()
                         join p in db.Positions on a.PositionId equals p.Id
                         join b in db.BusinessUnits on p.BusinessUnitId equals b.Id
                         where a.PersonId == person.Id && a.AssignmentType == AssignmentType.Primary
                               && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo > today)
                         let line = db.PositionReportingLines.FirstOrDefault(l => l.PositionId == p.Id && l.LineType == LineType.Solid
                               && l.EffectiveFrom <= today && (l.EffectiveTo == null || l.EffectiveTo > today))
                         let manager = db.Positions.FirstOrDefault(m => line != null && m.Id == line.ManagerPositionId)
                         let managerHolder = (from ma in db.PositionAssignments
                                              join mp in db.People on ma.PersonId equals mp.Id
                                              where line != null && ma.PositionId == line.ManagerPositionId
                                                    && ma.AssignmentType == AssignmentType.Primary
                                                    && ma.EffectiveFrom <= today && (ma.EffectiveTo == null || ma.EffectiveTo > today)
                                              select mp.FirstName + " " + mp.LastName).FirstOrDefault()
                         select new MyJob(p.Code, p.Title, b.Name, manager != null ? manager.Title : null, managerHolder))
                .FirstOrDefaultAsync(ct);

            if (permissions.Reach(Permissions.PeopleView) is { Team: true })
            {
                var people = await scope.PersonIdsAsync(Permissions.PeopleView, ct);
                teamSize = people is null ? 0 : people.Count(id => id != person.Id);
            }
        }

        var internalJobs = permissions.Has(Permissions.JobsBrowse)
            ? await db.JobPostings.CountAsync(j => j.Status == PostingStatus.Open && j.Visibility != PostingVisibility.External
                && j.OpenDate <= today && (j.CloseDate == null || j.CloseDate > today), ct)
            : 0;
        var myApplications = person is null ? 0 : await db.JobApplications.CountAsync(a => a.PersonId == person.Id, ct);

        return new DashboardDto(person?.FirstName ?? "there", stats, job, teamSize, internalJobs, myApplications);
    }
}
