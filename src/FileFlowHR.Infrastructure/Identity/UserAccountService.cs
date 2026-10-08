using System.Text;
using System.Text.Encodings.Web;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Email;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileFlowHR.Infrastructure.Identity;

/// <summary>Sign-in accounts on top of ASP.NET Core Identity. Always scoped to the current company.</summary>
public sealed class UserAccountService(
    UserManager<UserAccount> users,
    FileFlowHRDbContext db,
    ICompanyContext company,
    IEmailSender email,
    IOptions<AppOptions> options) : IUserAccountService
{
    private long CompanyId => company.CompanyId ?? throw new InvalidOperationException("No company context.");
    private IQueryable<UserAccount> CompanyUsers => users.Users.Where(u => u.CompanyId == CompanyId);

    public async Task<Result> InviteAsync(long personId, string emailAddress, CancellationToken ct)
    {
        if (await CompanyUsers.AnyAsync(u => u.PersonId == personId, ct))
            return Result.Invalid("This person already has a login. Use Resend invitation instead.");

        var companyRow = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == CompanyId, ct);
        var normalized = users.NormalizeEmail(emailAddress);
        if (await CompanyUsers.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            return Result.Invalid("Another login in this company already uses that email.");

        var isEmployee = await db.Employments.AnyAsync(e => e.PersonId == personId && e.TerminationDate == null, ct);
        var account = new UserAccount
        {
            CompanyId = CompanyId,
            PersonId = personId,
            UserName = UserAccount.MakeUserName(companyRow.Code, emailAddress),
            Email = emailAddress.Trim().ToLowerInvariant(),
            UserType = isEmployee ? UserTypes.Internal : UserTypes.External,
            Status = UserStatuses.Invited,
            LockoutEnabled = true,
        };
        var created = await users.CreateAsync(account);
        if (!created.Succeeded) return Result.Invalid([.. created.Errors.Select(e => e.Description)]);

        await SendInvitationAsync(account, companyRow, ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<string>> ValidatePasswordAsync(string password)
    {
        var errors = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, new UserAccount(), password);
            errors.AddRange(result.Errors.Select(e => e.Description));
        }
        return errors;
    }

    public async Task<Result<long>> CreateApplicantAsync(long companyId, string companyCode, long personId, string emailAddress,
        string password, CancellationToken ct)
    {
        var normalized = users.NormalizeEmail(emailAddress);
        if (await users.Users.AnyAsync(u => u.CompanyId == companyId && u.NormalizedEmail == normalized, ct))
            return Result<long>.Invalid("An account with this email already exists here. Sign in instead.");

        var account = new UserAccount
        {
            CompanyId = companyId,
            PersonId = personId,
            UserName = UserAccount.MakeUserName(companyCode, emailAddress),
            Email = emailAddress.Trim().ToLowerInvariant(),
            UserType = UserTypes.External,
            Status = UserStatuses.Active,
            LockoutEnabled = true,
        };
        var created = await users.CreateAsync(account, password);
        if (!created.Succeeded) return Result<long>.Invalid([.. created.Errors.Select(e => e.Description)]);

        var token = Encode(await users.GenerateEmailConfirmationTokenAsync(account));
        var link = $"{BaseUrl}/Account/ConfirmEmail?userId={account.Id}&code={token}";
        await email.SendAsync(account.Email, "Confirm your email",
            $"Thanks for registering with {HtmlEncoder.Default.Encode(companyCode)} careers. " +
            $"<a href='{HtmlEncoder.Default.Encode(link)}'>Confirm your email</a> to finish.", ct);
        return account.Id;
    }

    public async Task<Result> ResendInvitationAsync(long userAccountId, CancellationToken ct)
    {
        var account = await CompanyUsers.FirstOrDefaultAsync(u => u.Id == userAccountId, ct);
        if (account is null) return Result.NotFound("Login");
        if (account.Status != UserStatuses.Invited) return Result.Invalid("This person has already accepted their invitation.");
        var companyRow = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == CompanyId, ct);
        await SendInvitationAsync(account, companyRow, ct);
        return Result.Success();
    }

    public async Task<Result> SetEnabledAsync(long userAccountId, bool enabled, CancellationToken ct)
    {
        var account = await CompanyUsers.FirstOrDefaultAsync(u => u.Id == userAccountId, ct);
        if (account is null) return Result.NotFound("Login");
        if (enabled && account.Status == UserStatuses.Disabled)
            account.Status = account.EmailConfirmed ? UserStatuses.Active : UserStatuses.Invited;
        else if (!enabled)
            account.Status = UserStatuses.Disabled;

        await users.UpdateAsync(account);
        // A new security stamp signs the person out of every open session.
        if (!enabled) await users.UpdateSecurityStampAsync(account);
        return Result.Success();
    }

    public async Task DisableForPersonAsync(long personId, CancellationToken ct)
    {
        var account = await CompanyUsers.FirstOrDefaultAsync(u => u.PersonId == personId, ct);
        if (account is null || account.Status == UserStatuses.Disabled) return;
        account.Status = UserStatuses.Disabled;
        await users.UpdateAsync(account);
        await users.UpdateSecurityStampAsync(account);
    }

    public async Task MarkInternalAsync(long personId, CancellationToken ct)
    {
        var account = await CompanyUsers.FirstOrDefaultAsync(u => u.PersonId == personId, ct);
        if (account is null || account.UserType == UserTypes.Internal) return;
        account.UserType = UserTypes.Internal;
        await users.UpdateAsync(account);
    }

    public async Task<IReadOnlyList<UserAccountDto>> ListAsync(CancellationToken ct)
    {
        var list = await CompanyUsers.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var withExternal = await db.Set<UserExternalLogin>().AsNoTracking()
            .Where(l => list.Select(u => u.Id).Contains(l.UserId)).Select(l => l.UserId).ToListAsync(ct);
        return list.Select(u => ToDto(u, withExternal.Contains(u.Id))).ToList();
    }

    public async Task<UserAccountDto?> FindForPersonAsync(long personId, CancellationToken ct)
    {
        var account = await CompanyUsers.AsNoTracking().FirstOrDefaultAsync(u => u.PersonId == personId, ct);
        if (account is null) return null;
        var hasExternal = await db.Set<UserExternalLogin>().AnyAsync(l => l.UserId == account.Id, ct);
        return ToDto(account, hasExternal);
    }

    private static UserAccountDto ToDto(UserAccount u, bool hasExternal) => new(u.Id, u.PersonId, u.Email ?? "",
        u.UserType == UserTypes.External, u.Status, u.PasswordHash is not null, hasExternal, u.TwoFactorEnabled, u.LastSignInAt);

    private async Task SendInvitationAsync(UserAccount account, Company companyRow, CancellationToken ct)
    {
        var usesPassword = await db.CompanyLoginProviders.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyRow.Id
            && p.ProviderType == LoginProviderType.Password && p.IsEnabled, ct);
        var name = HtmlEncoder.Default.Encode(companyRow.LegalName);

        string body;
        if (usesPassword)
        {
            var token = Encode(await users.GeneratePasswordResetTokenAsync(account));
            var link = $"{BaseUrl}/Account/AcceptInvitation?userId={account.Id}&code={token}";
            body = $"{name} has invited you to FileFlowHR. <a href='{HtmlEncoder.Default.Encode(link)}'>Set your password</a> to start.";
        }
        else
        {
            var link = $"{BaseUrl}/Account/Login?company={companyRow.Code}";
            body = $"{name} has invited you to FileFlowHR. <a href='{HtmlEncoder.Default.Encode(link)}'>Sign in with your company account</a>.";
        }
        await email.SendAsync(account.Email!, $"Your FileFlowHR invitation from {companyRow.LegalName}", body, ct);
    }

    private string BaseUrl => options.Value.PublicBaseUrl.TrimEnd('/');
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
}
