// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.McpServer.Contracts.Responses;

/// <summary>
/// Represents a single message in a Flow conversation with full metadata and references.
/// This DTO materializes all relevant data for MCP clients without requiring additional queries.
/// </summary>
public record FlowMessageDTO
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The message content/text.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Source of the message (user, assistant, system).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// UTC timestamp when the message was last modified.
    /// </summary>
    [JsonPropertyName("modifiedUtc")]
    public DateTime? ModifiedUtc { get; init; }

    /// <summary>
    /// ID of the message this is replying to, if any.
    /// </summary>
    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; init; }

    /// <summary>
    /// Indicates if this is a Flow aggregation message combining multiple backend responses.
    /// </summary>
    [JsonPropertyName("isFlowAggregation")]
    public bool IsFlowAggregation { get; init; }

    /// <summary>
    /// Indicates if this is an intermediate aggregation message that has been superseded.
    /// </summary>
    [JsonPropertyName("isIntermediate")]
    public bool IsIntermediate { get; init; }

    /// <summary>
    /// ID of the message that supersedes this one, if any.
    /// </summary>
    [JsonPropertyName("supersededById")]
    public string? SupersededById { get; init; }

    /// <summary>
    /// Document processes that contributed to this message response.
    /// </summary>
    [JsonPropertyName("contributingProcesses")]
    public List<string> ContributingProcesses { get; init; } = new();

    /// <summary>
    /// Content references extracted from this message.
    /// These are documents, citations, or sources referenced in generating the response.
    /// </summary>
    [JsonPropertyName("contentReferences")]
    public List<FlowContentReferenceDTO> ContentReferences { get; init; } = new();

    /// <summary>
    /// Additional metadata specific to this message.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; init; } = new();
}