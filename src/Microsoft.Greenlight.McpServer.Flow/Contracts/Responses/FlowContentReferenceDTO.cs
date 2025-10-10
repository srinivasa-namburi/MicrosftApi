// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;

/// <summary>
/// Represents a content reference (document, citation, or source) used in generating a Flow response.
/// This DTO provides all necessary information for MCP clients to display or link to referenced content.
/// </summary>
public record FlowContentReferenceDTO
{
    /// <summary>
    /// Unique identifier for this reference.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Title or name of the referenced content.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Description or summary of the referenced content.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// URL or link to access the referenced content.
    /// May be a web URL, document path, or internal system reference.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// Type of reference (e.g., "document", "web", "database", "knowledge_base").
    /// </summary>
    [JsonPropertyName("referenceType")]
    public string ReferenceType { get; init; } = "document";

    /// <summary>
    /// The document process that provided this reference.
    /// </summary>
    [JsonPropertyName("sourceProcess")]
    public string? SourceProcess { get; init; }

    /// <summary>
    /// Relevance score of this reference to the query (0.0 to 1.0).
    /// Higher scores indicate more relevant content.
    /// </summary>
    [JsonPropertyName("relevanceScore")]
    public double? RelevanceScore { get; init; }

    /// <summary>
    /// Extracted content snippet from the reference.
    /// This is the actual text that was used in generating the response.
    /// </summary>
    [JsonPropertyName("extractedContent")]
    public string? ExtractedContent { get; init; }

    /// <summary>
    /// Page number or section identifier within the referenced document.
    /// </summary>
    [JsonPropertyName("pageOrSection")]
    public string? PageOrSection { get; init; }

    /// <summary>
    /// Additional metadata about the reference (e.g., author, date, tags).
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp when this reference was last accessed or validated.
    /// </summary>
    [JsonPropertyName("accessedUtc")]
    public DateTime? AccessedUtc { get; init; }
}