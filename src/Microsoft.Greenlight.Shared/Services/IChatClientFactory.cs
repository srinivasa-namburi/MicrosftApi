// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.AI;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Factory for obtaining <see cref="IChatClient"/> instances for a given deployment.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Gets (or creates) a chat client for the specified deployment.
    /// </summary>
    /// <param name="deploymentName">Azure OpenAI deployment name.</param>
    /// <param name="purposeTag">Optional tag used only for diagnostic naming.</param>
    /// <returns>The chat client.</returns>
    IChatClient GetChatClient(string deploymentName, string? purposeTag = null);
}
