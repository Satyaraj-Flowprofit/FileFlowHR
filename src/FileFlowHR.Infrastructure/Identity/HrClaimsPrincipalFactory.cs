using System.Security.Claims;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileFlowHR.Infrastructure.Identity;

/// <summary>
/// Builds the sign-in cookie: company, person, and every role the person holds today
/// (granted roles plus the automatic Employee, Manager and Applicant roles).
/// </summary>
public sealed class HrClaimsPrincipalFactory(UserManager<UserAccount> userManager, IOptions<IdentityOptions> options,
    FileFlowHRDbContext db, IRoleResolver roles)
    : UserClaimsPrincipalFactory<UserAccount>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserAccount user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == user.CompanyId);
        var person = user.PersonId is { } pid
            ? await db.People.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid && p.CompanyId == user.CompanyId)
            : null;

        identity.AddClaim(new Claim(HrClaims.UserAccountId, user.Id.ToString()));
        identity.AddClaim(new Claim(HrClaims.CompanyId, company.Id.ToString()));
        identity.AddClaim(new Claim(HrClaims.CompanyCode, company.Code));
        identity.AddClaim(new Claim(HrClaims.CompanyName, company.LegalName));
        identity.AddClaim(new Claim(HrClaims.UserType, user.UserType));
        identity.AddClaim(new Claim(HrClaims.DisplayName, person?.FullName ?? user.Email ?? ""));
        if (person is not null) identity.AddClaim(new Claim(HrClaims.PersonId, person.Id.ToString()));

        foreach (var role in await roles.ResolveAsync(company.Id, person?.Id))
            identity.AddClaim(new Claim(RoleAssignment.ClaimType, role.ToClaimValue()));

        // Password accounts must add a one-time code when the company requires it.
        var requireMfa = user.PasswordHash is not null && !user.TwoFactorEnabled &&
                         await db.CompanyLoginProviders.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == company.Id
                             && p.ProviderType == LoginProviderType.Password && p.IsEnabled && p.RequireMfa);
        if (requireMfa) identity.AddClaim(new Claim(HrClaims.MfaRequired, "true"));

        return identity;
    }
}
