using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Application.Features.People;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Recruitment;

public sealed record ApplicationRow(long Id, long PersonId, string PersonName, string? Email, string Source, ApplicationStatus Status,
    DateTime AppliedAt, IReadOnlyList<ApplicationStatus> NextSteps);

public sealed record PostingApplications(PostingDto Posting, IReadOnlyList<ApplicationRow> Applications);

[RequiresPermission(Permissions.ApplicationsManage)]
public sealed record ListApplicationsQuery(long JobPostingId) : IQuery<Result<PostingApplications>>;

public sealed class ListApplicationsHandler(IAppDbContext db, IAccessScopeResolver scope)
    : IRequestHandler<ListApplicationsQuery, Result<PostingApplications>>
{
    public async Task<Result<PostingApplications>> Handle(ListApplicationsQuery request, CancellationToken ct)
    {
        var j = await db.JobPostings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.JobPostingId, ct);
        var visible = await scope.PositionIdsAsync(Permissions.ApplicationsManage, ct);
        if (j is null || (visible is not null && !visible.Contains(j.PositionId))) return Result<PostingApplications>.NotFound("Job posting");

        var rows = await (from a in db.JobApplications.AsNoTracking()
                          join p in db.People on a.PersonId equals p.Id
                          where a.JobPostingId == j.Id
                          orderby a.AppliedAt
                          select new { a.Id, a.PersonId, Name = p.FirstName + " " + p.LastName, p.Email, a.Source, a.Status, a.AppliedAt })
            .ToListAsync(ct);
        var dto = new PostingDto(j.Id, j.PositionId, j.Title, j.Description, j.Visibility, j.OpenDate, j.CloseDate, j.Status);
        return new PostingApplications(dto, rows.Select(r => new ApplicationRow(r.Id, r.PersonId, r.Name, r.Email,
            r.Source.ToString(), r.Status, r.AppliedAt, JobApplication.NextSteps(r.Status))).ToList());
    }
}

/// <summary>Moves an application one step (screening, interview, offer, rejected). Hiring has its own command.</summary>
[RequiresPermission(Permissions.ApplicationsManage)]
public sealed record MoveApplicationCommand(long JobApplicationId, ApplicationStatus Status) : ICommand<Result>;

public sealed class MoveApplicationHandler(IAppDbContext db, IAccessScopeResolver scope) : IRequestHandler<MoveApplicationCommand, Result>
{
    public async Task<Result> Handle(MoveApplicationCommand request, CancellationToken ct)
    {
        if (request.Status == ApplicationStatus.Hired) return Result.Invalid("Use Hire to complete a hire.");
        var application = await db.JobApplications.FindAsync([request.JobApplicationId], ct);
        if (application is null) return Result.NotFound("Application");
        var posting = await db.JobPostings.AsNoTracking().FirstAsync(j => j.Id == application.JobPostingId, ct);
        var visible = await scope.PositionIdsAsync(Permissions.ApplicationsManage, ct);
        if (visible is not null && !visible.Contains(posting.PositionId)) return Result.NotFound("Application");

        application.MoveTo(request.Status);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Hires the applicant: marks the application hired, creates the employment and puts the person in
/// the posted position, marks the posting filled, and turns an applicant login into a staff login.
/// </summary>
[RequiresPermission(Permissions.ApplicationsManage)]
[RequiresPermission(Permissions.PeopleManage)]
public sealed record HireApplicantCommand(long JobApplicationId, DateOnly StartDate, EmploymentType EmploymentType) : ICommand<Result>;

public sealed class HireApplicantHandler(IAppDbContext db, IStaffingService staffing, TimeProvider clock)
    : IRequestHandler<HireApplicantCommand, Result>
{
    public async Task<Result> Handle(HireApplicantCommand request, CancellationToken ct)
    {
        var application = await db.JobApplications.FindAsync([request.JobApplicationId], ct);
        if (application is null) return Result.NotFound("Application");
        var posting = await db.JobPostings.FirstAsync(j => j.Id == application.JobPostingId, ct);

        application.MoveTo(ApplicationStatus.Hired);
        posting.MarkFilled(clock.Today());

        var isInternal = await db.Employments.AnyAsync(e => e.PersonId == application.PersonId && e.TerminationDate == null, ct);
        var placed = isInternal
            ? await staffing.AssignAsync(application.PersonId, posting.PositionId, AssignmentType.Primary, request.StartDate, null, ct)
            : await staffing.HireAsync(application.PersonId, posting.PositionId, request.StartDate, request.EmploymentType, ct);
        if (!placed.Succeeded) return placed;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
