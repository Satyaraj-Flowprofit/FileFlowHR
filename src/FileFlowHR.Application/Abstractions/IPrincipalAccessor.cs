using System.Security.Claims;

namespace FileFlowHR.Application.Abstractions;

/// <summary>Gives the dispatcher the signed-in user, from the Blazor circuit or the HTTP request.</summary>
public interface IPrincipalAccessor
{
    Task<ClaimsPrincipal> GetPrincipalAsync();
}
