// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Factory for obtaining <see cref="IChatCompletionService"/> instances for a given deployment.
/// Implementations should cache and reuse stateless chat completion service instances.
/// </summary>
public interface IChatCompletionServiceFactory
{
    /// <summary>
    /// Gets (or creates) a chat completion service for the specified deployment.
    /// </summary>
    /// <param name="deploymentName">Azure OpenAI deployment name.</param>
    /// <param name="purposeTag">Optional tag used only for diagnostic naming.</param>
    /// <returns>The chat completion service.</returns>
    IChatCompletionService GetChatService(string deploymentName, string? purposeTag = null);
}
