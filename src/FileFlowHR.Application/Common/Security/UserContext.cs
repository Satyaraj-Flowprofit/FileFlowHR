using System.Security.Claims;
using FileFlowHR.Application.Abstractions;

namespace FileFlowHR.Application.Common.Security;

/// <summary>
/// Scoped holder for the current user and company. Filled by the dispatcher (or the HTTP
/// pipeline) and read by the database filter, permission checks and handlers.
/// </summary>
public sealed class UserContext : ICurrentUser, ICompanyContext
{
    private long? _anonymousCompanyId;

    public bool IsAuthenticated { get; private set; }
    public long? UserAccountId { get; private set; }
    public long? CompanyId => IsAuthenticated ? _userCompanyId : _anonymousCompanyId;
    public string? CompanyCode { get; private set; }
    public long? PersonId { get; private set; }
    public bool IsExternal { get; private set; }
    public IReadOnlyCollection<RoleAssignment> RoleAssignments { get; private set; } = [];

    private long? _userCompanyId;

    public void SetUser(ClaimsPrincipal principal)
    {
        IsAuthenticated = principal.Identity?.IsAuthenticated == true
                          && long.TryParse(principal.FindFirst(HrClaims.CompanyId)?.Value, out _);
        if (!IsAuthenticated) return;

        UserAccountId = ParseLong(principal.FindFirst(HrClaims.UserAccountId)?.Value);
        _userCompanyId = ParseLong(principal.FindFirst(HrClaims.CompanyId)?.Value);
        CompanyCode = principal.FindFirst(HrClaims.CompanyCode)?.Value;
        PersonId = ParseLong(principal.FindFirst(HrClaims.PersonId)?.Value);
        IsExternal = principal.FindFirst(HrClaims.UserType)?.Value == "external";
        RoleAssignments = principal.FindAll(RoleAssignment.ClaimType)
            .Select(c => RoleAssignment.Parse(c.Value))
            .OfType<RoleAssignment>()
            .ToList();
    }

    /// <summary>
    /// For public pages (careers, applicant registration) where nobody is signed in yet.
    /// Signed-in users always stay in their own company.
    /// </summary>
    public void UseCompanyForAnonymous(long companyId)
    {
        if (IsAuthenticated)
            throw new InvalidOperationException("A signed-in user cannot switch company.");
        _anonymousCompanyId = companyId;
    }

    private static long? ParseLong(string? value) => long.TryParse(value, out var v) ? v : null;
}
