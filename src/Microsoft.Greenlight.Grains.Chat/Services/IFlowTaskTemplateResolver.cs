// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Grains.Chat.Services;

/// <summary>
/// Resolves Flow Task templates for a given user message.
/// </summary>
public interface IFlowTaskTemplateResolver
{
    /// <summary>
    /// Determines the Flow Task template that best matches the supplied message.
    /// </summary>
    /// <param name="message">The user message to analyze.</param>
    /// <param name="flowOptions">Current Flow configuration options.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The matching template, if any.</returns>
    Task<FlowTaskTemplateInfo?> DetermineFlowTaskTemplateAsync(
        string message,
        ServiceConfigurationOptions.FlowOptions flowOptions,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that evaluates vector similarity and trigger phrases.
/// </summary>
public sealed class FlowTaskTemplateResolver : IFlowTaskTemplateResolver
{
    private readonly IFlowTaskTemplateService _flowTaskTemplateService;
    private readonly IAiEmbeddingService _embeddingService;
    private readonly ISemanticKernelVectorStoreProvider _vectorStoreProvider;
    private readonly ILogger<FlowTaskTemplateResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskTemplateResolver"/> class.
    /// </summary>
    public FlowTaskTemplateResolver(
        IFlowTaskTemplateService flowTaskTemplateService,
        IAiEmbeddingService embeddingService,
        ISemanticKernelVectorStoreProvider vectorStoreProvider,
        ILogger<FlowTaskTemplateResolver> logger)
    {
        _flowTaskTemplateService = flowTaskTemplateService;
        _embeddingService = embeddingService;
        _vectorStoreProvider = vectorStoreProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FlowTaskTemplateInfo?> DetermineFlowTaskTemplateAsync(
        string message,
        ServiceConfigurationOptions.FlowOptions flowOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flowOptions);

        try
        {
            var availableTemplates = await _flowTaskTemplateService.GetActiveFlowTaskTemplatesAsync();
            if (!availableTemplates.Any())
            {
                return null;
            }

            var vectorMatch = await DetermineByVectorSimilarityAsync(message, availableTemplates, flowOptions);
            if (vectorMatch != null)
            {
                return vectorMatch;
            }

            foreach (var template in availableTemplates)
            {
                if (template.TriggerPhrases == null || !template.TriggerPhrases.Any())
                {
                    continue;
                }

                foreach (var phrase in template.TriggerPhrases)
                {
                    if (message.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Flow Task template '{TemplateName}' matched via trigger phrase '{Phrase}'",
                            template.Name,
                            phrase);
                        return template;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining Flow Task template");
            return null;
        }
    }

    private async Task<FlowTaskTemplateInfo?> DetermineByVectorSimilarityAsync(
        string message,
        List<FlowTaskTemplateInfo> availableTemplates,
        ServiceConfigurationOptions.FlowOptions flowOptions)
    {
        try
        {
            var messageEmbedding = await _embeddingService.GenerateEmbeddingsAsync(message);
            var minRelevance = flowOptions.RequireMinimumRelevanceForEngagement
                ? flowOptions.MinimumIntentRelevanceThreshold
                : 0.2;

            var searchResults = await _vectorStoreProvider.SearchAsync(
                indexName: SystemIndexes.FlowTaskTemplateIntentIndex,
                queryEmbedding: messageEmbedding,
                top: 5,
                minRelevance: minRelevance);

            FlowTaskTemplateInfo? bestMatch = null;
            double bestScore = 0.0;

            foreach (var match in searchResults)
            {
                if (!match.Record.Tags.TryGetValue("flowTaskTemplateId", out var templateIdValues) ||
                    !templateIdValues.Any())
                {
                    continue;
                }

                var templateIdString = templateIdValues.First();
                if (!Guid.TryParse(templateIdString, out var templateId))
                {
                    _logger.LogDebug("Skipping Flow Task vector match with invalid template id '{TemplateId}'", templateIdString);
                    continue;
                }

                var template = availableTemplates.FirstOrDefault(t => t.Id == templateId);
                if (template == null)
                {
                    _logger.LogDebug("Vector search returned template id {TemplateId} that is not active", templateId);
                    continue;
                }

                _logger.LogInformation(
                    "Flow Task template '{TemplateName}' has relevance score {Score:F3} (threshold: {Threshold:F3})",
                    template.Name,
                    match.Score,
                    flowOptions.MinimumIntentRelevanceThreshold);

                if (match.Score >= flowOptions.MinimumIntentRelevanceThreshold && match.Score > bestScore)
                {
                    bestScore = match.Score;
                    bestMatch = template;
                }
            }

            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "Flow Task template '{TemplateName}' meets threshold with score {Score:F3}",
                    bestMatch.Name,
                    bestScore);
            }
            else
            {
                _logger.LogDebug(
                    "No Flow Task template met the relevance threshold {Threshold:F3}",
                    flowOptions.MinimumIntentRelevanceThreshold);
            }

            return bestMatch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Flow Task vector similarity search failed");
            return null;
        }
    }
}
