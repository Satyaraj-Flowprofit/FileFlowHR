using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FileFlowHR.Infrastructure.Identity;

public enum ExternalSignInOutcome { SignedIn, NoAccount, Disabled, Failed }

public sealed record ExternalSignInResult(ExternalSignInOutcome Outcome, string? Email = null, string? CompanyName = null);

/// <summary>
/// After a company's own login system sends the user back: find their FileFlowHR account,
/// link it on first use (matched by email inside that company only) and sign them in.
/// Nobody can create an account this way; HR must have added them first.
/// </summary>
public sealed class ExternalLoginService(SignInManager<UserAccount> signIn, UserManager<UserAccount> users, FileFlowHRDbContext db)
{
    public async Task<ExternalSignInResult> SignInAsync(ExternalLoginInfo info)
    {
        var provider = await FindProviderAsync(info.LoginProvider);
        if (provider is null) return new(ExternalSignInOutcome.Failed);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == provider.CompanyId);

        var linked = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (linked is null)
        {
            var email = info.Principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? info.Principal.FindFirst("preferred_username")?.Value
                        ?? info.Principal.FindFirst(ClaimTypes.Upn)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return new(ExternalSignInOutcome.NoAccount, null, company.LegalName);

            var normalized = users.NormalizeEmail(email);
            linked = await users.Users.FirstOrDefaultAsync(u => u.CompanyId == company.Id && u.NormalizedEmail == normalized
                                                                && u.UserType == UserTypes.Internal);
            if (linked is null) return new(ExternalSignInOutcome.NoAccount, email, company.LegalName);
            if (linked.Status == UserStatuses.Disabled) return new(ExternalSignInOutcome.Disabled, email, company.LegalName);

            var added = await users.AddLoginAsync(linked, info);
            if (!added.Succeeded) return new(ExternalSignInOutcome.Failed, email, company.LegalName);
        }

        if (linked.CompanyId != company.Id) return new(ExternalSignInOutcome.Failed);
        if (linked.Status == UserStatuses.Disabled) return new(ExternalSignInOutcome.Disabled, linked.Email, company.LegalName);
        if (linked.Status == UserStatuses.Invited)
        {
            linked.Status = UserStatuses.Active;
            linked.EmailConfirmed = true;
            await users.UpdateAsync(linked);
        }

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        return result.Succeeded
            ? new(ExternalSignInOutcome.SignedIn, linked.Email, company.LegalName)
            : new(ExternalSignInOutcome.Failed, linked.Email, company.LegalName);
    }

    private async Task<CompanyLoginProvider?> FindProviderAsync(string schemeName)
    {
        var parts = schemeName.Split('-');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var id)) return null;
        return await db.CompanyLoginProviders.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsEnabled && p.ProviderType != LoginProviderType.Password);
    }
}
