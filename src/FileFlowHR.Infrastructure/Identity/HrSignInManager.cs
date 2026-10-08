using System.Security.Claims;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileFlowHR.Infrastructure.Identity;

/// <summary>Only active accounts of active companies may sign in; records the last sign-in time.</summary>
public sealed class HrSignInManager(
    UserManager<UserAccount> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<UserAccount> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<UserAccount>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<UserAccount> confirmation,
    FileFlowHRDbContext db,
    TimeProvider clock)
    : SignInManager<UserAccount>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(UserAccount user)
    {
        if (user.Status != UserStatuses.Active) return false;
        var companyActive = await db.Companies.AnyAsync(c => c.Id == user.CompanyId && c.Status == CompanyStatus.Active);
        return companyActive && await base.CanSignInAsync(user);
    }

    public override async Task SignInWithClaimsAsync(UserAccount user, AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        user.LastSignInAt = clock.GetUtcNow().UtcDateTime;
        await UserManager.UpdateAsync(user);
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
    }
}
