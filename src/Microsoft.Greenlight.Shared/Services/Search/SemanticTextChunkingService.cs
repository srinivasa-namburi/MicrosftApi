// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Advanced text chunking service that uses semantic boundaries and document structure awareness.
/// Provides more intelligent chunking than simple token-based splitting.
/// </summary>
public class SemanticTextChunkingService : ITextChunkingService
{
    private readonly ILogger<SemanticTextChunkingService> _logger;

    public SemanticTextChunkingService(ILogger<SemanticTextChunkingService> logger)
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

        // Normalize whitespace and clean up the content
        text = NormalizeContent(text);
        
        if (EstimateTokenCount(text) <= maxTokens)
        {
            return new List<string> { text };
        }

        var chunks = new List<string>();
        
        // Try to chunk by document structure first (headers, sections)
        var structuralChunks = ChunkByDocumentStructure(text, maxTokens, overlap);
        if (structuralChunks.Count > 1)
        {
            _logger.LogDebug("Using structural chunking for {ChunkCount} chunks", structuralChunks.Count);
            return structuralChunks;
        }

        // Fall back to semantic paragraph-based chunking
        var paragraphChunks = ChunkByParagraphs(text, maxTokens, overlap);
        if (paragraphChunks.Count > 1)
        {
            _logger.LogDebug("Using paragraph-based chunking for {ChunkCount} chunks", paragraphChunks.Count);
            return paragraphChunks;
        }

        // Final fallback to sentence-based chunking
        _logger.LogDebug("Using sentence-based chunking as fallback");
        return ChunkBySentences(text, maxTokens, overlap);
    }

    /// <inheritdoc />
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // More accurate token estimation based on word count and punctuation
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var punctuationCount = text.Count(c => char.IsPunctuation(c));
        
        // Approximation: most words are 1 token, punctuation adds tokens
        return words.Length + (punctuationCount / 4);
    }

    private static string NormalizeContent(string content)
    {
        // Replace multiple whitespace characters with single spaces
        content = Regex.Replace(content, @"[ \t]+", " ");
        
        // Preserve paragraph structure but normalize line breaks
        content = Regex.Replace(content, @"\r\n|\r|\n", "\n");
        content = Regex.Replace(content, @"\n\s*\n\s*\n+", "\n\n");
        
        return content.Trim();
    }

    private List<string> ChunkByDocumentStructure(string text, int maxTokens, int overlap)
    {
        // Look for markdown-style headers or structured content
        var headerPattern = @"^(#{1,6}\s+.+|[A-Z][^.\n]*:[ \t]*\n|\d+\.\s+[A-Z][^.\n]*|[A-Z][A-Z\s]{2,}[ \t]*\n)";
        var sections = Regex.Split(text, headerPattern, RegexOptions.Multiline)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (sections.Count <= 1)
        {
            return new List<string>(); // No clear structure found
        }

        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var section in sections)
        {
            var sectionTokens = EstimateTokenCount(section);
            
            // If adding this section would exceed the limit, finalize current chunk
            if (currentTokenCount + sectionTokens > maxTokens && currentChunk.Count > 0)
            {
                var chunk = string.Join("", currentChunk).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                // Handle overlap by keeping the last section if it's a header
                if (overlap > 0 && currentChunk.Count > 0)
                {
                    var lastSection = currentChunk.Last();
                    if (IsLikelyHeader(lastSection) && EstimateTokenCount(lastSection) <= overlap)
                    {
                        currentChunk = new List<string> { lastSection };
                        currentTokenCount = EstimateTokenCount(lastSection);
                    }
                    else
                    {
                        currentChunk.Clear();
                        currentTokenCount = 0;
                    }
                }
                else
                {
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }
            }

            // If this single section is too large, split it further
            if (sectionTokens > maxTokens)
            {
                var subChunks = ChunkBySentences(section, maxTokens, overlap);
                chunks.AddRange(subChunks);
            }
            else
            {
                currentChunk.Add(section);
                currentTokenCount += sectionTokens;
            }
        }

        // Add the final chunk
        if (currentChunk.Count > 0)
        {
            var finalChunk = string.Join("", currentChunk).Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks.Where(c => c.Length >= 10).ToList();
    }

    private List<string> ChunkByParagraphs(string text, int maxTokens, int overlap)
    {
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paragraphs.Count <= 1)
        {
            return new List<string>(); // No clear paragraph structure
        }

        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokens = EstimateTokenCount(paragraph);
            
            // If adding this paragraph would exceed the limit, finalize current chunk
            if (currentTokenCount + paragraphTokens > maxTokens && currentChunk.Count > 0)
            {
                var chunk = string.Join("\n\n", currentChunk).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                // Handle overlap by keeping some paragraphs from the end
                if (overlap > 0 && currentChunk.Count > 1)
                {
                    var overlapParagraphs = CreateOverlapParagraphs(currentChunk, overlap);
                    currentChunk = overlapParagraphs;
                    currentTokenCount = overlapParagraphs.Sum(EstimateTokenCount);
                }
                else
                {
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }
            }

            // If this single paragraph is too large, split it by sentences
            if (paragraphTokens > maxTokens)
            {
                var sentenceChunks = ChunkBySentences(paragraph, maxTokens, overlap);
                chunks.AddRange(sentenceChunks);
            }
            else
            {
                currentChunk.Add(paragraph);
                currentTokenCount += paragraphTokens;
            }
        }

        // Add the final chunk
        if (currentChunk.Count > 0)
        {
            var finalChunk = string.Join("\n\n", currentChunk).Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks.Where(c => c.Length >= 10).ToList();
    }

    private List<string> ChunkBySentences(string text, int maxTokens, int overlap)
    {
        var sentences = SplitIntoSentences(text);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokenCount = EstimateTokenCount(sentence);
            
            // If adding this sentence would exceed token limit and we have content
            if (currentTokenCount + sentenceTokenCount > maxTokens && currentChunk.Count > 0)
            {
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

            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokenCount;

            // If this single sentence is longer than max tokens, split by words
            if (sentenceTokenCount > maxTokens)
            {
                var wordChunks = ChunkLongSentence(sentence, maxTokens, overlap);
                chunks.AddRange(wordChunks);
                currentChunk.Clear();
                currentTokenCount = 0;
            }
        }

        // Add the final chunk
        if (currentChunk.Count > 0)
        {
            var finalChunk = string.Join(" ", currentChunk).Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks.Where(c => c.Length >= 10).ToList();
    }

    private static bool IsLikelyHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
            return false;

        // Check for markdown headers
        if (Regex.IsMatch(text, @"^#{1,6}\s+"))
            return true;

        // Check for numbered headers
        if (Regex.IsMatch(text, @"^\d+(\.\d+)*\.\s+[A-Z]"))
            return true;

        // Check for ALL CAPS headers
        if (text.Length < 100 && text.ToUpperInvariant() == text && text.Count(char.IsLetter) > 3)
            return true;

        // Check for title case without sentence-ending punctuation
        if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?') && 
            char.IsUpper(text[0]) && text.Split(' ').Length <= 10)
            return true;

        return false;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // More sophisticated sentence splitting with better abbreviation handling
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";
        var sentences = Regex.Split(text, sentencePattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        // Handle common abbreviations that shouldn't split sentences
        var mergedSentences = new List<string>();
        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            
            // Check if this looks like an abbreviation followed by a continuation
            if (i < sentences.Count - 1 && IsLikelyAbbreviationEnd(sentence))
            {
                sentence += " " + sentences[i + 1];
                i++; // Skip the next sentence since we merged it
            }
            
            mergedSentences.Add(sentence);
        }

        return mergedSentences;
    }

    private static bool IsLikelyAbbreviationEnd(string sentence)
    {
        var commonAbbreviations = new[] { "Dr.", "Mr.", "Mrs.", "Ms.", "Prof.", "Inc.", "Corp.", "Ltd.", "vs.", "etc.", "i.e.", "e.g." };
        return commonAbbreviations.Any(abbrev => sentence.TrimEnd().EndsWith(abbrev));
    }

    private List<string> CreateOverlapParagraphs(List<string> paragraphs, int overlapTokens)
    {
        if (paragraphs.Count == 0 || overlapTokens <= 0)
        {
            return new List<string>();
        }

        var overlapParagraphs = new List<string>();
        var currentTokens = 0;

        // Start from the end and work backwards
        for (int i = paragraphs.Count - 1; i >= 0; i--)
        {
            var paragraphTokens = EstimateTokenCount(paragraphs[i]);

            if (currentTokens + paragraphTokens <= overlapTokens)
            {
                overlapParagraphs.Insert(0, paragraphs[i]);
                currentTokens += paragraphTokens;
            }
            else
            {
                break;
            }
        }

        return overlapParagraphs;
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
                    var overlapWords = currentChunk.TakeLast(Math.Min(3, currentChunk.Count / 2)).ToList();
                    currentChunk = overlapWords;
                    currentTokenCount = overlapWords.Sum(EstimateTokenCount);
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
}
