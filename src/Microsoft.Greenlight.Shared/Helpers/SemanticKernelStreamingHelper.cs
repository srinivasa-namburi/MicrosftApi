// Copyright (c) Microsoft Corporation.
// Intentionally minimal dependencies; helper remains SK-agnostic beyond Streaming* types.

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.Text.RegularExpressions;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Helpers
{

    /// <summary>
    /// Result of processing a streaming update.
    /// </summary>
    public readonly record struct StreamingProcessingResult(StreamingProcessingAction Action, string Content = "");

    /// <summary>
    /// Helper for extracting only user-facing assistant text from Semantic Kernel streaming updates.
    /// Tool/function artifacts are ignored by design in this helper.
    /// </summary>
    public static class SemanticKernelStreamingHelper
    {
        /// <summary>
        /// Processes a streaming update and returns the action + any extracted text content.
        /// This centralizes exception handling and text extraction so callers can keep loops tight.
        /// </summary>
        private static StreamingProcessingResult ProcessStreamingUpdate(StreamingKernelContent update)
        {
            try
            {
                var content = ExtractContentFromStreamingUpdate(update);
                return new StreamingProcessingResult(StreamingProcessingAction.Continue, content);
            }
            catch (JsonException jsonEx) when (IsExpectedPluginJsonError(jsonEx))
            {
                // Expected with tool/function args mid-stream; don't break the loop.
                return new StreamingProcessingResult(StreamingProcessingAction.JsonErrorContinue);
            }
            catch
            {
                // Swallow and keep going; produce no text for this tick.
                return new StreamingProcessingResult(StreamingProcessingAction.Continue);
            }
        }

        /// <summary>
        /// Extracts only assistant text from an update.
        /// Ignores tool/function artifacts (StreamingFunctionCallUpdateContent, StreamingMethodContent, etc).
        /// </summary>
        private static string ExtractContentFromStreamingUpdate(StreamingKernelContent update)
        {
            try
            {
                switch (update)
                {
                    // ChatMessage wrapper that can interleave multiple item types.
                    case StreamingChatMessageContent chatUpdate:
                        {
                            var sb = new StringBuilder();

                            foreach (var item in chatUpdate.Items)
                            {
                                // We only surface text; everything else (function-call deltas/results) is ignored.
                                if (item is StreamingTextContent textItem && !string.IsNullOrEmpty(textItem.Text))
                                {
                                    sb.Append(textItem.Text);
                                }
                            }

                            // Fallback to convenience property if items carried no StreamingTextContent
                            if (sb.Length == 0 && !string.IsNullOrEmpty(chatUpdate.Content))
                            {
                                sb.Append(chatUpdate.Content);
                            }

                            return sb.ToString();
                        }

                    // Direct text chunk
                    case StreamingTextContent textUpdate:
                        return textUpdate.Text ?? string.Empty;

                    // Explicitly ignore function-call deltas/results
                    case StreamingFunctionCallUpdateContent:
                        return string.Empty;

                    // Some SK versions surface manufactured method content for function results.
                    // We ignore it on purpose to avoid surfacing tool outputs in this pipeline.
                    // case StreamingMethodContent _: return string.Empty;

                    default:
                        // Unknown/other content kinds -> no text.
                        return string.Empty;
                }
            }
            catch (JsonException)
            {
                // Partial JSON during tool-call arg streaming is normal; emit nothing for this tick.
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Convenience guard for expected mid-stream JSON exceptions while arguments/results are still incomplete.
        /// </summary>
        private static bool IsExpectedPluginJsonError(JsonException jsonEx)
        {
            var message = jsonEx.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("expected end of string")
                   || message.Contains("end of data")
                   || message.Contains("unexpectedend")
                   || message.Contains("line number")
                   || message.Contains("bytepositioninline")
                   || jsonEx.Path == "$";
        }

        /// <summary>
        /// True if the update type can contain user-facing text.
        /// </summary>
        public static bool IsTextContent(StreamingKernelContent update)
            => update is StreamingChatMessageContent or StreamingTextContent;

        /// <summary>
        /// Streams only assistant text from a generic SK stream that yields StreamingKernelContent.
        /// Tool artifacts are ignored. JSON parse blips are tolerated.
        /// </summary>
        public static async IAsyncEnumerable<string> StreamTextAsync(
            IAsyncEnumerable<StreamingKernelContent> stream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<string>? debugLog = null)
        {
            await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (JsonException jsonEx) when (IsExpectedPluginJsonError(jsonEx))
                {
                    debugLog?.Invoke($"[SK-Stream] Ignored expected JSON exception: {jsonEx.Message}");
                    continue;
                }

                if (!moved)
                {
                    yield break;
                }

                var update = enumerator.Current;
                var result = ProcessStreamingUpdate(update);
                if (result.Action == StreamingProcessingAction.Continue && !string.IsNullOrEmpty(result.Content))
                {
                    yield return result.Content;
                }
            }
        }

        /// <summary>
        /// Streams only assistant text from a stream that yields StreamingChatMessageContent.
        /// Tool artifacts are ignored. JSON parse blips are tolerated.
        /// </summary>
        public static async IAsyncEnumerable<string> StreamTextAsync(
            IAsyncEnumerable<StreamingChatMessageContent> stream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<string>? debugLog = null)
        {
            await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (JsonException jsonEx) when (IsExpectedPluginJsonError(jsonEx))
                {
                    debugLog?.Invoke($"[SK-ChatStream] Ignored expected JSON exception: {jsonEx.Message}");
                    continue;
                }

                if (!moved)
                {
                    yield break;
                }

                var update = enumerator.Current;
                var result = ProcessStreamingUpdate(update);
                if (result.Action == StreamingProcessingAction.Continue && !string.IsNullOrEmpty(result.Content))
                {
                    yield return result.Content;
                }
            }
        }

        /// <summary>
        /// Streams the first turn's assistant text and, if none arrives while a tool was invoked,
        /// triggers a caller-supplied fallback stream (e.g., a second mini-turn with autoInvoke:false).
        ///
        /// This keeps tool outputs hidden while ensuring the user still receives a final NL message.
        /// </summary>
        /// <param name="primaryStream">The initial SK streaming sequence.</param>
        /// <param name="wasToolInvoked">A delegate (e.g., backed by your FunctionInvocationFilter) that returns true iff at least one tool ran in the turn.</param>
        /// <param name="fallbackStreamFactory">Factory for a SECOND streaming sequence that must not auto-invoke tools (can be null to disable fallback).</param>
        /// <param name="cancellationToken">Cancellation.</param>
        /// <param name="debugLog">Optional debug logger.</param>
        public static async IAsyncEnumerable<string> StreamFinalTextWithFallbackAsync(
            IAsyncEnumerable<StreamingKernelContent> primaryStream,
            Func<bool> wasToolInvoked,
            Func<IAsyncEnumerable<StreamingKernelContent>>? fallbackStreamFactory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<string>? debugLog = null)
        {
            bool anyText = false;

            await foreach (var chunk in StreamTextAsync(primaryStream, cancellationToken, debugLog))
            {
                anyText = true;
                yield return chunk;
            }

            // If a tool ran and no text was produced, ask caller for a second turn.
            if (!anyText && wasToolInvoked() && fallbackStreamFactory is not null)
            {
                debugLog?.Invoke("[SK-Stream] No assistant text in primary turn but tool(s) ran; invoking fallback stream.");
                await foreach (var chunk in StreamTextAsync(fallbackStreamFactory(), cancellationToken, debugLog))
                {
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// Streams assistant text while manually handling function calls (tools).
        /// Auto function invocation is disabled on the provided execution settings for the duration of this call.
        /// The helper will:
        /// - Stream assistant text to the caller
        /// - Detect function call requests
        /// - Stop streaming, invoke the requested function via the Kernel and plugins
        /// - Append a tool result message to the chat history
        /// - Repeat until no further function calls are requested
        /// </summary>
        /// <param name="chatService">IChatCompletionService used for chat completions</param>
        /// <param name="chatHistory">Chat history to send and to which tool results will be appended</param>
        /// <param name="settings">Prompt execution settings to use (FunctionChoiceBehavior will be set to Auto(autoInvoke:false) and restored)</param>
        /// <param name="kernel">Kernel hosting plugins/tools</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="debugLog">Optional debug logger</param>
        public static async IAsyncEnumerable<string> StreamChatWithManualToolInvocationAsync(
            IChatCompletionService chatService,
            ChatHistory chatHistory,
            AzureOpenAIPromptExecutionSettings settings,
            Kernel kernel,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<string>? debugLog = null)
        {
            var originalBehavior = settings.FunctionChoiceBehavior;
            try
            {
                settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false);

                while (true)
                {
                    bool sawFunctionCall = false;
                    bool streamedAnyText = false;

                    await foreach (var part in chatService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, cancellationToken))
                    {
                        // Stream text pieces
                        if (part.Items is not null)
                        {
                            foreach (var t in part.Items.OfType<StreamingTextContent>())
                            {
                                if (!string.IsNullOrEmpty(t.Text))
                                {
                                    streamedAnyText = true;
                                    yield return t.Text;
                                }
                            }

                            // Detect any function-call streaming update
                            if (part.Items.OfType<StreamingFunctionCallUpdateContent>().Any())
                            {
                                sawFunctionCall = true;
                                debugLog?.Invoke("[SK-Tool] Function call detected in stream, stopping to process tools");
                                break; // stop this streaming turn; go run tool(s)
                            }
                        }
                    }

                    // If we didn't see a function call and either streamed some text or this is the first iteration,
                    // we're done with the conversation
                    if (!sawFunctionCall)
                    {
                        debugLog?.Invoke($"[SK-Tool] No function call detected. StreamedText: {streamedAnyText}. Ending conversation.");
                        yield break; // no more tools requested and stream completed
                    }

                    debugLog?.Invoke("[SK-Tool] Processing function calls...");

                    // Retrieve the consolidated function call with arguments via non-streaming call
                    var complete = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel, cancellationToken).ConfigureAwait(false);
                    var fnCalls = complete.Items?.OfType<FunctionCallContent>().ToList() ?? new List<FunctionCallContent>();
                    
                    if (!fnCalls.Any())
                    {
                        debugLog?.Invoke("[SK-Tool] Function call indicated in stream but none found in consolidated result.");
                        yield break; // nothing to do
                    }

                    // Add the assistant's response (containing function calls) to chat history
                    // This is required by the AI model to preserve context
                    chatHistory.Add(complete);
                    debugLog?.Invoke($"[SK-Tool] Added assistant message with {fnCalls.Count} function call(s) to chat history");

                    // Process each function call
                    foreach (var fnCall in fnCalls)
                    {
                        var pendingCallId = fnCall.Id;
                        var originalPluginName = fnCall.PluginName;
                        var originalFunctionName = fnCall.FunctionName;
                        
                        // Arguments are KernelArguments here; serialize to JSON string for logging / conversion
                        var pendingArgumentsJson = fnCall.Arguments is null ? null : JsonSerializer.Serialize(fnCall.Arguments);

                        debugLog?.Invoke($"[SK-Tool] Processing function call: OriginalPlugin='{originalPluginName}', OriginalFunction='{originalFunctionName}', CallId='{pendingCallId}'");

                        // Parse the function name to extract plugin and function names
                        // Handle cases where PluginName might be null and FunctionName contains the combined name
                        string resolvedPluginName;
                        string resolvedFunctionName;
                        
                        if (!string.IsNullOrWhiteSpace(originalPluginName) && !string.IsNullOrWhiteSpace(originalFunctionName))
                        {
                            // Standard case: both plugin and function names are populated
                            resolvedPluginName = originalPluginName;
                            resolvedFunctionName = originalFunctionName;
                        }
                        else if (!string.IsNullOrWhiteSpace(originalFunctionName))
                        {
                            // Parse combined function name (e.g., "DP__FacilitiesPlugin_GetLatLongForAddress")
                            // The plugin name includes the prefix, and the function name is just the last part after the final underscore
                            var lastUnderscoreIndex = originalFunctionName.LastIndexOf('_');
                            
                            if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < originalFunctionName.Length - 1)
                            {
                                // Plugin name is everything before the last underscore (including prefixes like "DP__")
                                // Function name is everything after the last underscore
                                resolvedPluginName = originalFunctionName.Substring(0, lastUnderscoreIndex);
                                resolvedFunctionName = originalFunctionName.Substring(lastUnderscoreIndex + 1);
                                debugLog?.Invoke($"[SK-Tool] Parsed function name: Plugin='{resolvedPluginName}', Function='{resolvedFunctionName}'");
                            }
                            else
                            {
                                // Cannot parse - treat as direct function call without plugin
                                debugLog?.Invoke($"[SK-Tool] Cannot parse function name '{originalFunctionName}', treating as direct function call");
                                resolvedPluginName = string.Empty;
                                resolvedFunctionName = originalFunctionName;
                            }
                        }
                        else
                        {
                            debugLog?.Invoke($"[SK-Tool] Skipping function call due to missing function name");
                            
                            // Add an error result to chat history
                            var errorResult = new FunctionResultContent(
                                functionName: "unknown",
                                pluginName: "unknown", 
                                callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                result: "Error: Missing function name");
                            var errorMessage = errorResult.ToChatMessage();
                            errorMessage.Role = AuthorRole.Tool;
                            chatHistory.Add(errorMessage);
                            continue;
                        }

                        debugLog?.Invoke($"[SK-Tool] Resolved function call: Plugin='{resolvedPluginName}', Function='{resolvedFunctionName}', CallId='{pendingCallId}'");

                        // Try to get the plugin and function
                        KernelFunction? kernelFunction = null;
                        
                        if (!string.IsNullOrWhiteSpace(resolvedPluginName))
                        {
                            // Try to find function within a specific plugin
                            if (!kernel.Plugins.TryGetPlugin(resolvedPluginName, out var plugin))
                            {
                                debugLog?.Invoke($"[SK-Tool] Plugin '{resolvedPluginName}' not found.");
                                
                                // Add an error result to chat history
                                var errorResult = new FunctionResultContent(
                                    functionName: resolvedFunctionName,
                                    pluginName: resolvedPluginName,
                                    callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                    result: $"Error: Plugin '{resolvedPluginName}' not found");
                                var errorMessage = errorResult.ToChatMessage();
                                errorMessage.Role = AuthorRole.Tool;
                                chatHistory.Add(errorMessage);
                                continue;
                            }
                            
                            if (!plugin.TryGetFunction(resolvedFunctionName, out kernelFunction))
                            {
                                debugLog?.Invoke($"[SK-Tool] Function '{resolvedPluginName}.{resolvedFunctionName}' not found.");
                                
                                // Add an error result to chat history
                                var errorResult = new FunctionResultContent(
                                    functionName: resolvedFunctionName,
                                    pluginName: resolvedPluginName,
                                    callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                    result: $"Error: Function '{resolvedFunctionName}' not found in plugin '{resolvedPluginName}'");
                                var errorMessage = errorResult.ToChatMessage();
                                errorMessage.Role = AuthorRole.Tool;
                                chatHistory.Add(errorMessage);
                                continue;
                            }
                        }
                        else
                        {
                            // Try to find function across all plugins when no plugin name is specified
                            debugLog?.Invoke($"[SK-Tool] Searching for function '{resolvedFunctionName}' across all plugins");
                            
                            foreach (var plugin in kernel.Plugins)
                            {
                                if (plugin.TryGetFunction(resolvedFunctionName, out kernelFunction))
                                {
                                    resolvedPluginName = plugin.Name;
                                    debugLog?.Invoke($"[SK-Tool] Found function '{resolvedFunctionName}' in plugin '{resolvedPluginName}'");
                                    break;
                                }
                            }
                            
                            if (kernelFunction == null)
                            {
                                debugLog?.Invoke($"[SK-Tool] Function '{resolvedFunctionName}' not found in any plugin.");
                                
                                // Add an error result to chat history
                                var errorResult = new FunctionResultContent(
                                    functionName: resolvedFunctionName,
                                    pluginName: "unknown",
                                    callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                    result: $"Error: Function '{resolvedFunctionName}' not found in any plugin");
                                var errorMessage = errorResult.ToChatMessage();
                                errorMessage.Role = AuthorRole.Tool;
                                chatHistory.Add(errorMessage);
                                continue;
                            }
                        }

                        // Invoke the function
                        try
                        {
                            var fnArgs = BuildKernelArgumentsFromJson(pendingArgumentsJson);
                            debugLog?.Invoke($"[SK-Tool] Invoking {resolvedPluginName}.{resolvedFunctionName} with args: {pendingArgumentsJson ?? "none"}");
                            
                            var result = await kernel.InvokeAsync(kernelFunction!, fnArgs, cancellationToken).ConfigureAwait(false);

                            // Append tool result as a TOOL message with FunctionResultContent
                            var fnResult = new FunctionResultContent(
                                functionName: resolvedFunctionName,
                                pluginName: resolvedPluginName,
                                callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                result: result?.GetValue<object>());
                            var toolResult = fnResult.ToChatMessage();
                            toolResult.Role = AuthorRole.Tool;
                            chatHistory.Add(toolResult);

                            debugLog?.Invoke($"[SK-Tool] Successfully invoked {resolvedPluginName}.{resolvedFunctionName}");
                        }
                        catch (Exception ex)
                        {
                            debugLog?.Invoke($"[SK-Tool] Error invoking {resolvedPluginName}.{resolvedFunctionName}: {ex.Message}");
                            
                            // Add an error result to chat history so the model can reason about the failure
                            var errorResult = new FunctionResultContent(
                                functionName: resolvedFunctionName,
                                pluginName: resolvedPluginName,
                                callId: pendingCallId ?? Guid.NewGuid().ToString(),
                                result: $"Error executing function: {ex.Message}");
                            var errorMessage = errorResult.ToChatMessage();
                            errorMessage.Role = AuthorRole.Tool;
                            chatHistory.Add(errorMessage);
                        }
                    }

                    debugLog?.Invoke($"[SK-Tool] Completed processing {fnCalls.Count} function call(s). Continuing conversation...");
                    // Loop reiterates; model will consume the tool result(s) now in history and potentially respond
                }
            }
            finally
            {
                settings.FunctionChoiceBehavior = originalBehavior;
            }
        }

        private static KernelArguments BuildKernelArgumentsFromJson(string? json)
        {
            var args = new KernelArguments();
            if (string.IsNullOrWhiteSpace(json))
            {
                return args;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        args[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => (object?)prop.Value.GetString(),
                            JsonValueKind.Number => TryGetNumber(prop.Value),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch
            {
                // Ignore malformed JSON
            }

            return args;
        }

        private static object TryGetNumber(JsonElement el)
        {
            if (el.TryGetInt64(out var l)) return l;
            if (el.TryGetDouble(out var d)) return d;
            return el.GetRawText();
        }
    }
}
