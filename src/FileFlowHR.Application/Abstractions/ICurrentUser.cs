using FileFlowHR.Application.Common.Security;

namespace FileFlowHR.Application.Abstractions;

/// <summary>Who is running the current request, read from the sign-in cookie's claims.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    long? UserAccountId { get; }
    long? CompanyId { get; }
    string? CompanyCode { get; }
    long? PersonId { get; }
    bool IsExternal { get; }
    IReadOnlyCollection<RoleAssignment> RoleAssignments { get; }
}

/// <summary>The company whose rows the database may return. Fails closed when unknown.</summary>
public interface ICompanyContext
{
    long? CompanyId { get; }
}
