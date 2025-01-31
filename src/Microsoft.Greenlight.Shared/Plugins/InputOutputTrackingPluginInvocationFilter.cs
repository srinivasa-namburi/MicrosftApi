using System.Text.Json;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.SemanticKernel;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// A filter that tracks the input and output of plugin function invocations.
/// </summary>
public class InputOutputTrackingPluginInvocationFilter : IFunctionInvocationFilter
{
    private readonly IPluginSourceReferenceCollector _pluginSourceReferenceCollector;
    private Guid _executionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputOutputTrackingPluginInvocationFilter"/> class.
    /// </summary>
    /// <param name="pluginSourceReferenceCollector">The collector for plugin source references.</param>
    public InputOutputTrackingPluginInvocationFilter(IPluginSourceReferenceCollector pluginSourceReferenceCollector)
    {
        _pluginSourceReferenceCollector = pluginSourceReferenceCollector;
        _executionId = Guid.Empty;
    }

    /// <summary>
    /// Invoked when a function is called, tracking its input and output.
    /// </summary>
    /// <param name="context">The context of the function invocation.</param>
    /// <param name="next">The next function to call in the invocation pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // Don't track ContentState function calls as there are internal calls that we don't want to track.
        if (context.Function.PluginName != null && context.Function.PluginName.Contains("ContentState"))
        {
            await next(context);
            return;
        }

        // Don't track system functions or functions that don't have an execution ID associated with them.
        // We have two methods of determining the System-ExecutionId. 
        // This first method is for functions that are called from GenericAiCompletionService
        if (context.Function.PluginName == null)
        {
            // We're executing the main function call. We can get the Execution ID from the context.
            if (context.Arguments.Any(a => a.Key == "System-ExecutionId"))
            {
                var value = context.Arguments["System-ExecutionId"]!.ToString();
                _executionId = Guid.Parse(value!);
            }

            // For this way of determining execution ID, we skip tracking the execution for the rest of this
            // initial (system) function call.
            await next(context);
            return;
        }

        // This second method is for functions that are called from the AgentAiCompletionService
        // This method doesn't require us to skip tracking the execution for the rest of the function call, since the execution id
        // is passed with every agent function call.
        if (context.Kernel.Data.TryGetValue("System-ExecutionId", out object? systemExecutionIdValue))
        {
            var value = systemExecutionIdValue!.ToString();
            _executionId = Guid.Parse(value!);
        }



        var pluginSourceReferenceItem = new PluginSourceReferenceItem();

        if (_executionId != Guid.Empty)
        {
            // Function has not yet been called.
            pluginSourceReferenceItem.SetBasicParameters();
            pluginSourceReferenceItem.PluginIdentifier = context.Function.PluginName + "_" + context.Function.Name;

            var functionInputs = new Dictionary<string, string>();

            // Capture the input of the function
            foreach (var argument in context.Arguments)
            {
                if (argument is { Key: "System-ExecutionId", Value: Guid })
                {
                    _executionId = argument.Value as Guid? ?? Guid.Empty;
                    continue;
                }

                if (argument.Value is string)
                {
                    try
                    {
                        functionInputs.Add(argument.Key, argument.Value.ToString() ?? throw new InvalidOperationException());
                    }
                    catch (InvalidOperationException)
                    {
                        functionInputs.Add(argument.Key, "Unknown Parameter");
                    }
                }
                else
                {
                    var argumentJson = JsonSerializer.Serialize(argument.Value);
                    functionInputs.Add(argument.Key, argumentJson);
                }
            }

            try
            {
                pluginSourceReferenceItem.SourceInputJson = JsonSerializer.Serialize(functionInputs);
            }
            catch (JsonException)
            {
                pluginSourceReferenceItem.SourceInputJson = "Couldn't serialize plugin input to JSON";
            }
        }

        // Call the function.
        await next(context);

        if (_executionId != Guid.Empty)
        {
            // Set the output of the function as the source output
            if (context.Result.ValueType == typeof(string))
            {
                var functionOutput = context.Result.GetValue<string>();
                if (!string.IsNullOrEmpty(functionOutput))
                {
                    pluginSourceReferenceItem.SetSourceOutput(functionOutput);
                }
            }
            else
            {
                var functionOutput = context.Result.GetValue<object>();
                if (functionOutput != null)
                {
                    try
                    {
                        pluginSourceReferenceItem.SetSourceOutput(JsonSerializer.Serialize(functionOutput));
                    }
                    catch (JsonException)
                    {
                        // If the output is not a string, and cannot be serialized to JSON, then set the output as a string
                        pluginSourceReferenceItem.SetSourceOutput("Couldn't serialize plugin output to JSON");
                    }
                }
            }

            if (_executionId != Guid.Empty)
            {
                // Add the source reference item to the collector if we have a valid execution ID
                _pluginSourceReferenceCollector.Add(_executionId, pluginSourceReferenceItem);
            }
        }
    }
}
