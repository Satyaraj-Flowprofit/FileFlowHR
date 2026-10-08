using System.Collections.Concurrent;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common.Security;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlowHR.Application.Common.Messaging;

/// <summary>
/// Runs each request in its own DI scope, so every use case gets a fresh DbContext. This matters in
/// Blazor Server, where a circuit lives for a long time and a shared DbContext would not be safe.
/// The signed-in user is copied into the new scope before anything runs.
/// </summary>
public sealed class Dispatcher(IServiceScopeFactory scopeFactory, IPrincipalAccessor principalAccessor) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IResult, IResultFactory<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var principal = await principalAccessor.GetPrincipalAsync();

        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<UserContext>().SetUser(principal);

        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(request.GetType(), static requestType =>
            Activator.CreateInstance(typeof(RequestWrapper<,>).MakeGenericType(requestType, typeof(TResponse)))!);

        return await wrapper.Handle(request, scope.ServiceProvider, cancellationToken);
    }

    private abstract class RequestWrapper<TResponse> where TResponse : IResult, IResultFactory<TResponse>
    {
        public abstract Task<TResponse> Handle(object request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class RequestWrapper<TRequest, TResponse> : RequestWrapper<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResult, IResultFactory<TResponse>
    {
        public override Task<TResponse> Handle(object request, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typed, ct);

            // Behaviours run in registration order: the first registered is the outermost.
            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = pipeline;
                pipeline = () => behavior.Handle(typed, next, ct);
            }
            return pipeline();
        }
    }
}
