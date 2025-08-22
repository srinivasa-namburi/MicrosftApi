// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Text; // Direct SK TextChunker usage

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Service for chunking text content into smaller pieces for embedding and vector storage.
/// Provides functionality similar to what Kernel Memory does automatically for chunking.
/// </summary>
#pragma warning disable SKEXP0001 // Experimental SK API
#pragma warning disable SKEXP0010 // Additional experimental analyzer
#pragma warning disable SKEXP0050 // Evaluation-only API usage
public class ChunkingService : ITextChunkingService
{
    private readonly ILogger<ChunkingService>? _logger;

    /// <summary>
    /// Creates a new instance of ChunkingService.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public ChunkingService(ILogger<ChunkingService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<string> ChunkText(string content, int maxTokens = 1000, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<string>();
        }

        // Normalize whitespace and clean up the content
        content = NormalizeContent(content);

        // Attempt to use Semantic Kernel TextChunker directly (best-effort, swallow failures, fallback to internal logic)
        try
        {

            // Provide conservative per-line token cap so later merge respects maxTokens
            var lines = TextChunker.SplitPlainTextLines(content, Math.Max(50, maxTokens / 10));

            if (lines is not null)
            {
                var merged = MergeLinesIntoChunks(lines, maxTokens, overlap);
                if (merged.Count > 0)
                {
                    _logger?.LogDebug("Used SK TextChunker for {ChunkCount} chunks", merged.Count);
                    return merged;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Direct SK TextChunker call failed, falling back to internal implementation");
        }

        if (EstimateTokenCount(content) <= maxTokens)
        {
            return new List<string> { content };
        }

        var chunks = new List<string>();
        var sentences = SplitIntoSentences(content);

        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokenCount = EstimateTokenCount(sentence);

            // If adding this sentence would exceed token limit and we have content
            if (currentTokenCount + sentenceTokenCount > maxTokens && currentChunk.Count > 0)
            {
                // Create chunk from current sentences
                var chunk = string.Join(" ", currentChunk).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                // Handle overlap by keeping some sentences from the end
                if (overlap > 0 && currentChunk.Count > 1)
                {
                    var overlapSentences = CreateOverlapSentences(currentChunk, overlap);
                    currentChunk = overlapSentences;
                    currentTokenCount = overlapSentences.Sum(EstimateTokenCount);
                }
                else
                {
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }
            }

            // If this single sentence is longer than max tokens, handle it specially
            if (sentenceTokenCount > maxTokens)
            {
                // Split long sentence by words
                var wordChunks = ChunkLongSentence(sentence, maxTokens, overlap);
                chunks.AddRange(wordChunks);
                continue; // Skip adding this sentence to the current chunk
            }

            // Add the current sentence
            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokenCount;
        }

        // Add the final chunk if there's remaining content
        if (currentChunk.Count > 0)
        {
            var finalChunk = string.Join(" ", currentChunk).Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        // Filter out very short chunks (less than 10 characters)
        var result = chunks.Where(c => c.Length >= 10).ToList();

        _logger?.LogDebug("Chunked text into {ChunkCount} chunks from {OriginalLength} characters",
            result.Count, content.Length);

        return result;
    }

    /// <inheritdoc />
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // Rough approximation: 1 token ≈ 4 characters for English text
        // This is a simplification - a proper tokenizer would be more accurate
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static string NormalizeContent(string content)
    {
        // Replace multiple whitespace characters with single spaces
        content = Regex.Replace(content, @"\s+", " ");

        // Remove excessive line breaks but preserve paragraph structure
        content = Regex.Replace(content, @"\n\s*\n\s*\n+", "\n\n");

        return content.Trim();
    }

    private static List<string> SplitIntoSentences(string content)
    {
        // Split on sentence boundaries (periods, exclamation marks, question marks)
        // but be careful about abbreviations and decimal numbers
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";
        var sentences = Regex.Split(content, sentencePattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    private List<string> CreateOverlapSentences(List<string> sentences, int overlapTokens)
    {
        if (sentences.Count == 0 || overlapTokens <= 0)
        {
            return new List<string>();
        }

        var overlapSentences = new List<string>();
        var currentTokens = 0;

        // Start from the end and work backwards to get the most recent context
        for (int i = sentences.Count - 1; i >= 0; i--)
        {
            var sentenceTokens = EstimateTokenCount(sentences[i]);

            if (currentTokens + sentenceTokens <= overlapTokens)
            {
                overlapSentences.Insert(0, sentences[i]);
                currentTokens += sentenceTokens;
            }
            else
            {
                break;
            }
        }

        return overlapSentences;
    }

    private List<string> ChunkLongSentence(string sentence, int maxTokens, int overlapTokens)
    {
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var word in words)
        {
            var wordTokenCount = EstimateTokenCount(word);

            if (currentTokenCount + wordTokenCount > maxTokens && currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));

                // Create overlap for long sentences
                if (overlapTokens > 0 && currentChunk.Count > 1)
                {
                    var overlapWords = new List<string>();
                    var overlapTokenCount = 0;

                    for (int i = currentChunk.Count - 1; i >= 0; i--)
                    {
                        var currentChunkWordTokenCount = EstimateTokenCount(currentChunk[i]);
                        if (overlapTokenCount + currentChunkWordTokenCount <= overlapTokens)
                        {
                            overlapWords.Insert(0, currentChunk[i]);
                            overlapTokenCount += currentChunkWordTokenCount;
                        }
                        else
                        {
                            break;
                        }
                    }

                    currentChunk = overlapWords;
                    currentTokenCount = overlapTokenCount;
                }
                else
                {
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }
            }

            currentChunk.Add(word);
            currentTokenCount += wordTokenCount;
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(" ", currentChunk));
        }

        return chunks;
    }

    private List<string> MergeLinesIntoChunks(IEnumerable<string> lines, int maxTokens, int overlap)
    {
        var prelim = lines.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (prelim.Count == 0) return new List<string>();
        var merged = new List<string>();
        var current = new List<string>();
        var currentTokens = 0;
        foreach (var line in prelim)
        {
            var tokens = EstimateTokenCount(line);
            if (currentTokens + tokens > maxTokens && current.Count > 0)
            {
                merged.Add(string.Join(" ", current));
                if (overlap > 0 && current.Count > 1)
                {
                    var overlapSentences = CreateOverlapSentences(current, overlap);
                    current = overlapSentences;
                    currentTokens = overlapSentences.Sum(EstimateTokenCount);
                }
                else
                {
                    current.Clear();
                    currentTokens = 0;
                }
            }
            current.Add(line);
            currentTokens += tokens;
        }
        if (current.Count > 0)
        {
            merged.Add(string.Join(" ", current));
        }
        return merged;
    }
}
