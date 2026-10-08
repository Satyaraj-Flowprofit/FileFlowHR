using FileFlowHR.Application.Abstractions;

namespace FileFlowHR.Application.Common.Security;

public interface IPermissionService
{
    bool Has(string permission);

    /// <summary>The widest reach the user has for this permission, merged across all their roles.</summary>
    PermissionReach Reach(string permission);
}

/// <summary>Where a permission applies. Company = everything; otherwise the listed areas.</summary>
public sealed record PermissionReach(
    bool Company, IReadOnlySet<long> RegionIds, IReadOnlySet<long> BusinessUnitIds, bool Team, bool Self)
{
    public static readonly PermissionReach None = new(false, new HashSet<long>(), new HashSet<long>(), false, false);
    public bool Any => Company || RegionIds.Count > 0 || BusinessUnitIds.Count > 0 || Team || Self;
}

/// <summary>Pure logic over the user's role claims and the role matrix (no database).</summary>
public sealed class PermissionService(ICurrentUser user) : IPermissionService
{
    public bool Has(string permission) => Reach(permission).Any;

    public PermissionReach Reach(string permission)
    {
        if (!user.IsAuthenticated) return PermissionReach.None;

        bool company = false, team = false, self = false;
        var regions = new HashSet<long>();
        var units = new HashSet<long>();

        foreach (var assignment in user.RoleAssignments)
        {
            if (!RolePermissions.Matrix.TryGetValue(assignment.Role, out var allowed) ||
                !allowed.TryGetValue(permission, out var maxScope))
                continue;

            // A grant never reaches further than the role allows. Automatic roles carry Company
            // and take the permission's own limit (for example Self or Team).
            AccessScope scope;
            if (assignment.Scope <= maxScope) scope = assignment.Scope;
            else if (maxScope is AccessScope.Team or AccessScope.Self) scope = maxScope;
            else continue; // area-limited permission granted too widely: fail closed
            switch (scope)
            {
                case AccessScope.Company: company = true; break;
                case AccessScope.Region when assignment.ScopeId is { } r: regions.Add(r); break;
                case AccessScope.BusinessUnit when assignment.ScopeId is { } b: units.Add(b); break;
                case AccessScope.Team: team = true; break;
                case AccessScope.Self: self = true; break;
            }
        }
        return new PermissionReach(company, regions, units, team, self);
    }
}
