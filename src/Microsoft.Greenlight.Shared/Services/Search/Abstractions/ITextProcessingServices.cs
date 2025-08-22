// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Service for extracting text content from various file types.
/// </summary>
public interface ITextExtractionService
{
    /// <summary>
    /// Extracts text content from a file stream.
    /// </summary>
    /// <param name="fileStream">Stream containing the file data.</param>
    /// <param name="fileName">Name of the file (used to determine file type).</param>
    /// <returns>Extracted text content.</returns>
    Task<string> ExtractTextAsync(Stream fileStream, string fileName);

    /// <summary>
    /// Checks if the service supports the given file type.
    /// </summary>
    /// <param name="fileName">Name of the file to check.</param>
    /// <returns>True if the file type is supported.</returns>
    bool SupportsFileType(string fileName);
}

/// <summary>
/// Service for chunking text content into smaller pieces for vector storage.
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Chunks text content into smaller pieces.
    /// </summary>
    /// <param name="text">Text to chunk.</param>
    /// <param name="maxTokens">Maximum tokens per chunk.</param>
    /// <param name="overlap">Number of overlapping tokens between chunks.</param>
    /// <returns>List of text chunks.</returns>
    List<string> ChunkText(string text, int maxTokens = 1000, int overlap = 100);

    /// <summary>
    /// Estimates the number of tokens in a text string.
    /// </summary>
    /// <param name="text">Text to analyze.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokenCount(string text);
}
