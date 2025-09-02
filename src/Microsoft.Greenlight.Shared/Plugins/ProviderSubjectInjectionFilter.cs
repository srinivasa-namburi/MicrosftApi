using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// Ensures that the ambient UserExecutionContext carries the ProviderSubjectId from the Kernel.Data for the duration of a function call.
/// </summary>
public sealed class ProviderSubjectInjectionFilter : IFunctionInvocationFilter
{
    /// <summary>
    /// SK filter hook.
    /// </summary>
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var kernel = context.Kernel;
        string? previous = UserExecutionContext.ProviderSubjectId;
        try
        {
            if (kernel != null && kernel.Data.TryGetValue(KernelUserContextConstants.ProviderSubjectId, out var value))
            {
                if (value is string sVal && !string.IsNullOrWhiteSpace(sVal))
                {
                    UserExecutionContext.ProviderSubjectId = sVal;
                }
            }
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            UserExecutionContext.ProviderSubjectId = previous;
        }
    }
}
