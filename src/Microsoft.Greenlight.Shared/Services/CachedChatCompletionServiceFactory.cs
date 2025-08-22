// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection; // For FromKeyedServices
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.Services;

// Transitional note: Until an official Azure OpenAI IChatClient exists we still materialize an SK chat service
// and adapt it to IChatClient. This keeps public surface purely IChatClient while hiding SK types.
/// <summary>
/// Default caching implementation of <see cref="IChatClientFactory"/>. Internally creates a Semantic Kernel
/// <see cref="IChatCompletionService"/> per deployment and adapts it to <see cref="IChatClient"/> until
/// Microsoft.Extensions.AI supplies a first-class Azure OpenAI chat client type. Public callers only see
/// the <see cref="IChatClient"/> abstraction.
/// </summary>
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public sealed class CachedChatClientFactory : IChatClientFactory
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly ILogger<CachedChatClientFactory> _logger;
    private readonly ConcurrentDictionary<string, IChatCompletionService> _skCache = new();
    private readonly ConcurrentDictionary<string, IChatClient> _chatClientCache = new();

    /// <summary>Creates a new <see cref="CachedChatClientFactory"/>.</summary>
    /// <param name="openAIClient">Keyed Azure OpenAI client.</param>
    /// <param name="logger">Logger instance.</param>
    public CachedChatClientFactory(
        [FromKeyedServices("openai-planner")] AzureOpenAIClient openAIClient,
        ILogger<CachedChatClientFactory> logger)
    {
        _openAIClient = openAIClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public IChatClient GetChatClient(string deploymentName, string? purposeTag = null)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new ArgumentException("Deployment name must be provided", nameof(deploymentName));
        }

        return _chatClientCache.GetOrAdd(deploymentName, key =>
        {
            _logger.LogInformation("Creating AzureOpenAIChatCompletionService for deployment {Deployment} (tag: {Tag})", key, purposeTag);
            var sk = _skCache.GetOrAdd(key, d => new AzureOpenAIChatCompletionService(d, _openAIClient, purposeTag ?? $"openai-{d}"));
#pragma warning disable SKEXP0010
            return sk.AsChatClient();
#pragma warning restore SKEXP0010
        });
    }
}
