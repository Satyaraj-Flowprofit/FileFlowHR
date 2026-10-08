using FileFlowHR.Domain.Security;

namespace FileFlowHR.Application.Common.Security;

/// <summary>A role the user holds right now, with the area it covers. Stored in the sign-in cookie.</summary>
public sealed record RoleAssignment(string Role, AccessScope Scope, long? ScopeId)
{
    public const string ClaimType = "hr:role";

    public string ToClaimValue() => $"{Role}|{Scope}|{ScopeId}";

    public static RoleAssignment? Parse(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3 || !Enum.TryParse<AccessScope>(parts[1], out var scope)) return null;
        return new RoleAssignment(parts[0], scope, long.TryParse(parts[2], out var id) ? id : null);
    }

    public static string RoleCode(GrantableRole role) => role switch
    {
        GrantableRole.ClientAdmin => Roles.ClientAdmin,
        GrantableRole.HrAdmin => Roles.HrAdmin,
        GrantableRole.Recruiter => Roles.Recruiter,
        GrantableRole.Auditor => Roles.Auditor,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static AccessScope FromGrantScope(ScopeType scopeType) => scopeType switch
    {
        ScopeType.Company => AccessScope.Company,
        ScopeType.Region => AccessScope.Region,
        ScopeType.BusinessUnit => AccessScope.BusinessUnit,
        _ => throw new ArgumentOutOfRangeException(nameof(scopeType)),
    };
}
