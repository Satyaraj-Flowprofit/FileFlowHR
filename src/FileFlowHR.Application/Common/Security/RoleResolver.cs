using FileFlowHR.Application.Abstractions;
using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Common.Security;

public interface IRoleResolver
{
    /// <summary>All roles a person holds today: granted ones plus Employee, Manager and Applicant.</summary>
    Task<IReadOnlyList<RoleAssignment>> ResolveAsync(long companyId, long? personId, CancellationToken ct = default);
}

/// <summary>
/// Works out automatic roles from HR data at sign-in:
/// Employee = open employment; Manager = holds a position that another position reports to
/// with a solid line; Applicant = has any job application.
/// </summary>
public sealed class RoleResolver(IAppDbContext db, TimeProvider clock) : IRoleResolver
{
    public async Task<IReadOnlyList<RoleAssignment>> ResolveAsync(long companyId, long? personId, CancellationToken ct = default)
    {
        if (personId is not { } pid) return [];
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var roles = new List<RoleAssignment>();

        // Explicit CompanyId filters: at sign-in there is no company context yet.
        var isEmployee = await db.Employments.IgnoreQueryFilters()
            .AnyAsync(e => e.CompanyId == companyId && e.PersonId == pid && e.HireDate <= today
                           && (e.TerminationDate == null || e.TerminationDate > today), ct);
        if (isEmployee)
        {
            roles.Add(new RoleAssignment(Roles.Employee, AccessScope.Company, null));

            var heldPositions = db.PositionAssignments.IgnoreQueryFilters()
                .Where(a => a.CompanyId == companyId && a.PersonId == pid && a.EffectiveFrom <= today
                            && (a.EffectiveTo == null || a.EffectiveTo > today))
                .Select(a => a.PositionId);
            var isManager = await db.PositionReportingLines.IgnoreQueryFilters()
                .AnyAsync(l => l.CompanyId == companyId && l.LineType == LineType.Solid
                               && heldPositions.Contains(l.ManagerPositionId)
                               && l.EffectiveFrom <= today && (l.EffectiveTo == null || l.EffectiveTo > today), ct);
            if (isManager) roles.Add(new RoleAssignment(Roles.Manager, AccessScope.Company, null));
        }

        // Anyone with a login who is not current staff can apply for public jobs (new applicants, former staff).
        var isApplicant = !isEmployee || await db.JobApplications.IgnoreQueryFilters()
            .AnyAsync(a => a.CompanyId == companyId && a.PersonId == pid, ct);
        if (isApplicant) roles.Add(new RoleAssignment(Roles.Applicant, AccessScope.Company, null));

        // Granted roles only count while the person still works here.
        if (isEmployee)
        {
            var grants = await db.RoleGrants.IgnoreQueryFilters()
                .Where(g => g.CompanyId == companyId && g.PersonId == pid && g.EffectiveFrom <= today
                            && (g.EffectiveTo == null || g.EffectiveTo > today))
                .ToListAsync(ct);
            roles.AddRange(grants.Select(g => new RoleAssignment(
                RoleAssignment.RoleCode(g.RoleCode), RoleAssignment.FromGrantScope(g.ScopeType), g.ScopeId)));
        }
        return roles;
    }
}
