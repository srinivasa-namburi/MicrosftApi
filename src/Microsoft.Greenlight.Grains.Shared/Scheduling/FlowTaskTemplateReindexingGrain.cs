// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Orleans.Concurrency;
using System.Text;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

/// <summary>
/// Scheduled grain that reindexes Flow Task template metadata for intent detection.
/// Runs every 30 minutes to keep the system index up to date with template changes.
/// Uses SemanticKernelVectorStoreProvider directly for system indexes.
/// </summary>
[Reentrant]
public class FlowTaskTemplateReindexingGrain : Grain, IFlowTaskTemplateReindexingGrain
{
    private readonly ILogger<FlowTaskTemplateReindexingGrain> _logger;
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskTemplateReindexingGrain"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="sp">Service provider for dependency resolution.</param>
    public FlowTaskTemplateReindexingGrain(
        ILogger<FlowTaskTemplateReindexingGrain> logger,
        IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Flow Task template metadata reindexing job started at {Time}", DateTimeOffset.Now);

        try
        {
            using var scope = _sp.CreateScope();
            var flowTaskTemplateService = scope.ServiceProvider.GetRequiredService<IFlowTaskTemplateService>();
            var vectorStoreProvider = scope.ServiceProvider.GetRequiredService<ISemanticKernelVectorStoreProvider>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IAiEmbeddingService>();

            // Get all active Flow Task templates
            var activeTemplates = await flowTaskTemplateService.GetActiveFlowTaskTemplatesAsync();

            if (!activeTemplates.Any())
            {
                _logger.LogInformation("No active Flow Task templates found for metadata indexing");
                return;
            }

            _logger.LogInformation("Found {TemplateCount} active Flow Task templates for metadata reindexing", activeTemplates.Count);

            // Generate a sample embedding to determine dimensions
            _logger.LogDebug("Generating sample embedding to determine dimensions");
            var sampleEmbedding = await embeddingService.GenerateEmbeddingsAsync("sample");
            var dimensions = sampleEmbedding.Length;
            _logger.LogDebug("Sample embedding generated. Dimensions: {Dimensions}", dimensions);

            // Ensure collection exists with correct dimensions
            _logger.LogDebug("Ensuring collection exists: {CollectionName} with {Dimensions} dimensions",
                SystemIndexes.FlowTaskTemplateIntentIndex, dimensions);
            await vectorStoreProvider.EnsureCollectionAsync(SystemIndexes.FlowTaskTemplateIntentIndex, dimensions);
            _logger.LogDebug("Collection ensured");

            // Clear existing records to avoid duplicates from schema changes or metadata updates
            _logger.LogDebug("Clearing existing records from collection {CollectionName}", SystemIndexes.FlowTaskTemplateIntentIndex);
            await vectorStoreProvider.ClearCollectionAsync(SystemIndexes.FlowTaskTemplateIntentIndex);
            _logger.LogDebug("Collection cleared");

            // Build records for all templates
            var records = new List<SkVectorChunkRecord>();
            foreach (var template in activeTemplates)
            {
                try
                {
                    var record = await BuildVectorRecordAsync(template, embeddingService);
                    records.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error building vector record for Flow Task template {TemplateName}", template.Name);
                }
            }

            if (records.Any())
            {
                // Upsert all records in a single batch
                await vectorStoreProvider.UpsertAsync(SystemIndexes.FlowTaskTemplateIntentIndex, records);
                _logger.LogInformation("Flow Task template metadata reindexing completed successfully. Indexed {TemplateCount} templates.", records.Count);
            }
            else
            {
                _logger.LogWarning("No Flow Task template records were built successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Flow Task template metadata reindexing");
            // Non-fatal - will retry on next scheduled run
        }
    }

    /// <summary>
    /// Builds a vector chunk record for a single Flow Task template.
    /// </summary>
    private async Task<SkVectorChunkRecord> BuildVectorRecordAsync(FlowTaskTemplateInfo template, IAiEmbeddingService embeddingService)
    {
        _logger.LogDebug("Building vector record for template: {TemplateName}", template.Name);

        // Build comprehensive, query-friendly metadata text for vector embedding
        // Use natural language and multiple phrasings to improve semantic matching
        // Front-load the most important search terms for better similarity
        var metadataBuilder = new StringBuilder();

        // Process trigger phrases first: strip punctuation and add variations
        var processedPhrases = new List<string>();
        if (template.TriggerPhrases != null && template.TriggerPhrases.Any())
        {
            processedPhrases = template.TriggerPhrases
                .Select(p => StripPunctuation(p))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }

        // Front-load trigger phrases in query-like formats for maximum similarity
        if (processedPhrases.Any())
        {
            // Repeat key phrases multiple times in different natural phrasings
            foreach (var phrase in processedPhrases)
            {
                metadataBuilder.AppendLine($"I need to {phrase}");
                metadataBuilder.AppendLine($"Help me {phrase}");
                metadataBuilder.AppendLine($"I want to {phrase}");
            }

            // Add direct keyword lines
            metadataBuilder.AppendLine(string.Join(" ", processedPhrases));
            metadataBuilder.AppendLine(string.Join(", ", processedPhrases));
        }

        // Add conversational description
        metadataBuilder.AppendLine($"This template helps you {template.DisplayName}.");
        metadataBuilder.AppendLine($"Use this to {template.DisplayName}.");

        if (!string.IsNullOrWhiteSpace(template.Description))
        {
            metadataBuilder.AppendLine(template.Description);
        }

        // Add template identifiers
        metadataBuilder.AppendLine($"Template: {template.Name}");
        metadataBuilder.AppendLine($"Category: {template.Category}");
        metadataBuilder.AppendLine($"Sections: {template.SectionCount}, Requirements: {template.TotalRequirementCount}");

        var metadataText = metadataBuilder.ToString();
        _logger.LogDebug("Metadata text built for {TemplateName}, length: {Length}", template.Name, metadataText.Length);

        // Generate embedding for the metadata
        var embedding = await embeddingService.GenerateEmbeddingsAsync(metadataText);
        _logger.LogDebug("Embedding generated for {TemplateName}, dimensions: {Dimensions}", template.Name, embedding.Length);

        // Build tags dictionary with List<string?> values as required by SkVectorChunkRecord
        var tags = new Dictionary<string, List<string?>>
        {
            ["flowTaskTemplateId"] = new List<string?> { template.Id.ToString() },
            ["flowTaskTemplateName"] = new List<string?> { template.Name },
            ["category"] = new List<string?> { template.Category },
            ["metadataType"] = new List<string?> { "flow-task-intent-detection" },
            ["systemIndex"] = new List<string?> { "true" },
            ["lastReindexed"] = new List<string?> { DateTime.UtcNow.ToString("O") }
        };

        return new SkVectorChunkRecord
        {
            DocumentId = template.Id.ToString(),
            FileName = $"template-{template.Name}.metadata",
            DisplayFileName = $"{template.DisplayName} Template",
            ChunkText = metadataText,
            Embedding = embedding,
            PartitionNumber = 0, // Each template is a single chunk
            IngestedAt = DateTimeOffset.UtcNow,
            Tags = tags,
            DocumentReference = template.Id.ToString()
        };
    }

    /// <summary>
    /// Strips punctuation from a string while preserving spaces and alphanumeric characters.
    /// This improves semantic similarity matching for trigger phrases.
    /// </summary>
    private static string StripPunctuation(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var result = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            // Keep letters, digits, and spaces; remove all punctuation
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                result.Append(c);
            }
        }

        // Normalize whitespace (replace multiple spaces with single space)
        return System.Text.RegularExpressions.Regex.Replace(result.ToString(), @"\s+", " ").Trim();
    }
}
