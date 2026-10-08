using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FileFlowHR.Application.Common.Behaviors;

/// <summary>Turns broken business rules into readable failures instead of error pages.</summary>
public sealed class ExceptionBehavior<TRequest, TResponse>(ILogger<ExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult, IResultFactory<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (DomainException ex)
        {
            return TResponse.Fail(FailureKind.Invalid, [ex.Message]);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TResponse.Fail(FailureKind.Invalid, ["Someone else changed this record. Reload and try again."]);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Database rejected {Request}", typeof(TRequest).Name);
            return TResponse.Fail(FailureKind.Invalid,
                ["The change breaks a data rule (for example, a duplicate code or two current holders of one position)."]);
        }
    }
}
