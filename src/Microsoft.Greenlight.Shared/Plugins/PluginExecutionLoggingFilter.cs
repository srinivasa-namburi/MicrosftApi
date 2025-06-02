// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// Logs plugin function invocations in Semantic Kernel, including inputs and outputs (up to 1024 chars).
/// </summary>
public class PluginExecutionLoggingFilter : IFunctionInvocationFilter
{
    private const int MaxLogLength = 1024;
    private readonly ILogger<PluginExecutionLoggingFilter> _logger;

    public PluginExecutionLoggingFilter(ILogger<PluginExecutionLoggingFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        if ((context.Function.Name.StartsWith("InvokePrompt", StringComparison.OrdinalIgnoreCase)))
             
        {
            await next(context);
            return;
        }

        string inputString = string.Join(", ", context.Arguments.Select(kvp => $"{kvp.Key}={Truncate(kvp.Value?.ToString(), 128)}"));
        inputString = Truncate(inputString, MaxLogLength / 2);
        _logger.LogInformation("Invoking plugin: {PluginName} Function: {FunctionName} | Inputs: {Inputs}", context.Function.PluginName, context.Function.Name, inputString);

        try
        {
            await next(context);
            // Try to get the output from the context if available
            string? outputString = null;
            if (context.Function.PluginName != null && (context.Function.PluginName.StartsWith("DocumentHistory", StringComparison.OrdinalIgnoreCase) ||
                                                        context.Function.PluginName.StartsWith("ContentState", StringComparison.OrdinalIgnoreCase)))
            {
                outputString = "Executed successfully";
            }
            else
            {
                outputString = Truncate(context.Result.ToString(), MaxLogLength / 2);
            }
            _logger.LogInformation("Plugin {PluginName} Function {FunctionName} completed successfully | Output: {Output}", context.Function.PluginName, context.Function.Name, outputString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {PluginName} Function {FunctionName} threw an exception", context.Function.PluginName, context.Function.Name);
            throw;
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
    }
}