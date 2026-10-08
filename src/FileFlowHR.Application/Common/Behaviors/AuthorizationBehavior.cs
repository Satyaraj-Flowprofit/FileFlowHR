using System.Reflection;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;

namespace FileFlowHR.Application.Common.Behaviors;

/// <summary>
/// Checks the permission on the request before the handler runs. The UI hides buttons too,
/// but this server-side check is the one that counts.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser, IPermissionService permissions)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult, IResultFactory<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var type = typeof(TRequest);
        if (type.GetCustomAttribute<AllowAnonymousRequestAttribute>() is not null)
            return next();

        if (!currentUser.IsAuthenticated || currentUser.CompanyId is null)
            return Task.FromResult(TResponse.Fail(FailureKind.Forbidden, ["Please sign in."]));

        var required = type.GetCustomAttributes<RequiresPermissionAttribute>().Select(a => a.Permission).ToList();
        if (required.Count == 0 && type.GetCustomAttribute<RequiresSignInAttribute>() is null)
            throw new InvalidOperationException($"{type.Name} must declare a permission, [RequiresSignIn] or [AllowAnonymousRequest].");

        return required.All(permissions.Has)
            ? next()
            : Task.FromResult(TResponse.Fail(FailureKind.Forbidden, ["You do not have access to do this."]));
    }
}
