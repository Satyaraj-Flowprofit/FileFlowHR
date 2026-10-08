using FileFlowHR.Application.Common;

namespace FileFlowHR.Application.Abstractions;

public sealed record UserAccountDto(
    long UserAccountId, long? PersonId, string Email, bool IsExternal, string Status,
    bool HasPassword, bool HasCompanyLogin, bool TwoFactorEnabled, DateTime? LastSignInAt);

/// <summary>
/// Sign-in accounts. Implemented with ASP.NET Core Identity in Infrastructure, so the use cases
/// never touch password hashing, tokens or lockout directly.
/// </summary>
public interface IUserAccountService
{
    /// <summary>Creates an invited account for a person in the current company and emails a link.</summary>
    Task<Result> InviteAsync(long personId, string email, CancellationToken ct);

    /// <summary>Creates an active external (applicant) account with a password; sends an email confirmation.</summary>
    Task<Result<long>> CreateApplicantAsync(long companyId, string companyCode, long personId, string email, string password, CancellationToken ct);

    /// <summary>Checks a password against the password rules without saving anything.</summary>
    Task<IReadOnlyList<string>> ValidatePasswordAsync(string password);

    Task<Result> ResendInvitationAsync(long userAccountId, CancellationToken ct);
    Task<Result> SetEnabledAsync(long userAccountId, bool enabled, CancellationToken ct);
    Task DisableForPersonAsync(long personId, CancellationToken ct);
    Task MarkInternalAsync(long personId, CancellationToken ct);
    Task<IReadOnlyList<UserAccountDto>> ListAsync(CancellationToken ct);
    Task<UserAccountDto?> FindForPersonAsync(long personId, CancellationToken ct);
}

/// <summary>Tells the web host that a company's login settings changed, so sign-in buttons stay current.</summary>
public interface ILoginProviderChangeNotifier
{
    Task ProvidersChangedAsync(CancellationToken ct);
}
