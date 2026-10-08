using System.Security.Claims;
using FileFlowHR.Application.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace FileFlowHR.Web.Security;

/// <summary>
/// The signed-in user for the dispatcher: from the Blazor circuit when there is one,
/// otherwise from the HTTP request (static pages and endpoints).
/// </summary>
public sealed class PrincipalAccessor(AuthenticationStateProvider authStateProvider, IHttpContextAccessor http) : IPrincipalAccessor
{
    public async Task<ClaimsPrincipal> GetPrincipalAsync()
    {
        try
        {
            return (await authStateProvider.GetAuthenticationStateAsync()).User;
        }
        catch (InvalidOperationException)
        {
            return http.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
