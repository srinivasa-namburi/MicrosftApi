// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Basic text chunking service that splits text into overlapping chunks.
/// </summary>
public class BasicTextChunkingService : ITextChunkingService
{
    private readonly ILogger<BasicTextChunkingService> _logger;

    public BasicTextChunkingService(ILogger<BasicTextChunkingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<string> ChunkText(string text, int maxTokens = 1000, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        var chunks = new List<string>();
        var sentences = SplitIntoSentences(text);

        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokenCount(sentence);

            // If adding this sentence would exceed the limit, finalize current chunk
            if (currentTokenCount + sentenceTokens > maxTokens && currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));

                // Start new chunk with overlap
                var overlapChunk = CreateOverlapChunk(currentChunk, overlap);
                currentChunk = overlapChunk;
                currentTokenCount = overlapChunk.Sum(EstimateTokenCount);
            }

            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokens;
        }

        // Add the final chunk if it has content
        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(" ", currentChunk));
        }

        _logger.LogDebug("Chunked text into {ChunkCount} chunks from {OriginalLength} characters",
            chunks.Count, text.Length);

        return chunks;
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

    private List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Basic sentence splitting - can be improved with more sophisticated rules
        var sentenceEnders = new[] { '.', '!', '?' };
        var sentences = new List<string>();
        var currentSentence = new List<char>();

        for (int i = 0; i < text.Length; i++)
        {
            var currentChar = text[i];
            currentSentence.Add(currentChar);

            if (sentenceEnders.Contains(currentChar))
            {
                // Check if this is likely the end of a sentence
                bool isEndOfSentence = true;

                // Don't split on abbreviations like "Dr.", "Mr.", etc.
                if (currentChar == '.' && i > 0)
                {
                    var precedingChars = new string(currentSentence.ToArray());
                    if (precedingChars.EndsWith("Dr.") || precedingChars.EndsWith("Mr.") ||
                        precedingChars.EndsWith("Mrs.") || precedingChars.EndsWith("Ms.") ||
                        precedingChars.EndsWith("Prof.") || precedingChars.EndsWith("Inc.") ||
                        precedingChars.EndsWith("Corp.") || precedingChars.EndsWith("Ltd."))
                    {
                        isEndOfSentence = false;
                    }
                }

                if (isEndOfSentence)
                {
                    var sentence = new string(currentSentence.ToArray()).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    currentSentence.Clear();
                }
            }
        }

        // Add any remaining content as the last sentence
        if (currentSentence.Count > 0)
        {
            var sentence = new string(currentSentence.ToArray()).Trim();
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    private List<string> CreateOverlapChunk(List<string> sentences, int overlapTokens)
    {
        if (sentences.Count == 0 || overlapTokens <= 0)
        {
            return new List<string>();
        }

        var overlapChunk = new List<string>();
        var currentTokens = 0;

        // Start from the end and work backwards to get the most recent context
        for (int i = sentences.Count - 1; i >= 0; i--)
        {
            var sentenceTokens = EstimateTokenCount(sentences[i]);

            if (currentTokens + sentenceTokens <= overlapTokens)
            {
                overlapChunk.Insert(0, sentences[i]);
                currentTokens += sentenceTokens;
            }
            else
            {
                break;
            }
        }

        return overlapChunk;
    }
}
