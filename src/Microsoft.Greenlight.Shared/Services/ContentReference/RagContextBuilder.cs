using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Models;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Service for building RAG contexts from content references
    /// </summary>
    public class RagContextBuilder : IRagContextBuilder
    {
        private readonly IContentReferenceService _contentReferenceService;
        private readonly IAiEmbeddingService _aiEmbeddingService;
        private readonly ILogger<RagContextBuilder> _logger;

        public RagContextBuilder(
            IContentReferenceService contentReferenceService,
            IAiEmbeddingService aiEmbeddingService,
            ILogger<RagContextBuilder> logger)
        {
            _contentReferenceService = contentReferenceService;
            _aiEmbeddingService = aiEmbeddingService;
            _logger = logger;
        }


        /// <inheritdoc />
        public async Task<string> BuildContextWithSelectedReferencesAsync(
            string userQuery, 
            List<ContentReferenceItem> allReferences, 
            int topN = 5,
            int maxChunkTokens = 1200)
        {
            var contextStringBuilder = new StringBuilder();
            contextStringBuilder.AppendLine("[Context]");
            contextStringBuilder.AppendLine("The following are pre-rendered chunks of parts of the document(s) picked to answer the user's question:");

            if (allReferences.Any())
            {
                try
                {
                    // Get all embeddings for references (will use cached if available)
                    var allEmbeddings = await _contentReferenceService.GetOrCreateEmbeddingsForContentAsync(
                        allReferences, maxChunkTokens);

                    if (allEmbeddings.Any())
                    {
                        // Generate query embedding
                        var queryEmbedding = await _aiEmbeddingService.GenerateEmbeddingsAsync(userQuery);

                        // Calculate similarity scores
                        var scores = new List<(string Chunk, float Score)>();
                        foreach (var entry in allEmbeddings)
                        {
                            var chunk = entry.Key.Chunk;
                            var embedding = entry.Value;
                            var score = _aiEmbeddingService.CalculateCosineSimilarity(queryEmbedding, embedding);
                            scores.Add((chunk, score));
                        }

                        // Get top chunks by similarity score
                        var topChunks = scores
                            .OrderByDescending(x => x.Score)
                            .Take(topN)
                            .Select(x => x.Chunk)
                            .ToList();

                        // Add top chunks to context
                        foreach (var chunk in topChunks)
                        {
                            contextStringBuilder.AppendLine(chunk);
                            contextStringBuilder.AppendLine();
                        }
                    }
                    else
                    {
                        // Fallback handling if no embeddings are available
                        AddFallbackChunks(contextStringBuilder, allReferences);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building context with selected references");
                    AddErrorFallback(contextStringBuilder, allReferences);
                }
            }
            else
            {
                contextStringBuilder.AppendLine("No references available for this conversation.");
            }

            contextStringBuilder.AppendLine("[/Context]");
            return contextStringBuilder.ToString();
        }

        /// <summary>
        /// Adds fallback chunks when no embeddings are available
        /// </summary>
        private void AddFallbackChunks(StringBuilder contextStringBuilder, List<ContentReferenceItem> references)
        {
            _logger.LogWarning("No embeddings available, using fallback content selection");
            
            // Include first chunk from each reference
            foreach (var reference in references.Take(3))
            {
                contextStringBuilder.AppendLine($"--- Reference: {reference.DisplayName ?? $"Item {reference.Id}"} ---");
                
                if (!string.IsNullOrEmpty(reference.RagText))
                {
                    var firstChunk = reference.RagText.Length > 1000
                        ? reference.RagText.Substring(0, 1000) + "..."
                        : reference.RagText;
                    contextStringBuilder.AppendLine(firstChunk);
                }
                else
                {
                    contextStringBuilder.AppendLine($"Description: {reference.Description ?? "No description available"}");
                }
                
                contextStringBuilder.AppendLine();
            }
        }

        /// <summary>
        /// Adds error fallback information when an exception occurs
        /// </summary>
        private void AddErrorFallback(StringBuilder contextStringBuilder, List<ContentReferenceItem> references)
        {
            // Basic fallback info
            contextStringBuilder.AppendLine("Error processing references. Available references:");
            
            foreach (var reference in references.Take(5))
            {
                contextStringBuilder.AppendLine($"- {reference.DisplayName ?? $"Item {reference.Id}"}");
            }
        }
    }
}
