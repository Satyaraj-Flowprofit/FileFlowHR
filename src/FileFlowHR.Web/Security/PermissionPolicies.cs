using System.Security.Claims;
using FileFlowHR.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace FileFlowHR.Web.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Pages and buttons use [Authorize(Policy = Permissions.X)] or AuthorizeView Policy="...".</summary>
public static class PermissionPolicies
{
    public static void Register(AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
            options.AddPolicy(permission, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
    }

    public static bool Has(ClaimsPrincipal user, string permission)
    {
        var context = new UserContext();
        context.SetUser(user);
        return new PermissionService(context).Has(permission);
    }
}

/// <summary>Same rules as the server-side check in the use cases, so the UI never shows what would be refused.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (PermissionPolicies.Has(context.User, requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
