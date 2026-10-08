using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using Microsoft.AspNetCore.Components;

namespace FileFlowHR.Web.Components.Shared;

/// <summary>
/// Base for app pages: runs use cases through the dispatcher and keeps the last error or success
/// message for the page to show. Pages contain no business rules of their own.
/// </summary>
public abstract class HrComponentBase : ComponentBase
{
    [Inject] protected IDispatcher Dispatcher { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected string? ErrorMessage { get; set; }
    protected string? SuccessMessage { get; set; }
    protected bool Busy { get; private set; }

    /// <summary>Runs a command or query; on failure the message is shown and default is returned.</summary>
    protected async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, string? successMessage = null)
        where TResponse : FileFlowHR.Application.Common.IResult, IResultFactory<TResponse>
    {
        Busy = true;
        try
        {
            var result = await Dispatcher.Send(request);
            if (result.Succeeded)
            {
                ErrorMessage = null;
                if (successMessage is not null) SuccessMessage = successMessage;
            }
            else
            {
                SuccessMessage = null;
                ErrorMessage = string.Join(" ", result.Errors);
            }
            return result;
        }
        finally
        {
            Busy = false;
        }
    }

    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }
}
