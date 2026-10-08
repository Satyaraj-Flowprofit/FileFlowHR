namespace FileFlowHR.Application.Common.Messaging;

/// <summary>A request handled by exactly one handler. Responses are always a Result.</summary>
public interface IRequest<TResponse> where TResponse : IResult, IResultFactory<TResponse>;

/// <summary>Changes data. (CQRS: commands and queries are kept apart.)</summary>
public interface ICommand<TResponse> : IRequest<TResponse> where TResponse : IResult, IResultFactory<TResponse>;

/// <summary>Reads data and never changes it.</summary>
public interface IQuery<TResponse> : IRequest<TResponse> where TResponse : IResult, IResultFactory<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult, IResultFactory<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Cross-cutting step around every handler (validation, permissions, error mapping).</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult, IResultFactory<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>The single entry point the UI uses to run use cases (mediator pattern).</summary>
public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IResult, IResultFactory<TResponse>;
}

/// <summary>Marks a request with the permission a user needs to run it.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

/// <summary>Marks a request anyone may run, including people who are not signed in.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AllowAnonymousRequestAttribute : Attribute;

/// <summary>Marks a request any signed-in user may run; the handler checks ownership itself.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresSignInAttribute : Attribute;
