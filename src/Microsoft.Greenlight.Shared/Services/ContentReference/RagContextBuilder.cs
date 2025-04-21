using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Enums;
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
                    // Check if we have a content editing reference (first reference with specific name)
                    var contentBeingEdited = allReferences.FirstOrDefault(r =>
                        r.DisplayName?.Contains("Content Being Edited") == true ||
                        r.ReferenceType == ContentReferenceType.GeneratedSection);

                    if (contentBeingEdited != null && !string.IsNullOrEmpty(contentBeingEdited.RagText))
                    {
                        // Always include the content being edited at the top
                        contextStringBuilder.AppendLine("[CONTENT BEING EDITED]");
                        contextStringBuilder.AppendLine(contentBeingEdited.RagText);
                        contextStringBuilder.AppendLine("[/CONTENT BEING EDITED]");
                        contextStringBuilder.AppendLine();

                        // Remove transient references from the list we'll process for embeddings
                        allReferences = allReferences.Where(r => r.Id != contentBeingEdited.Id).ToList();

                        // Decrease topN by 1 since we're including the edited content separately
                        if (topN > 1) topN--;
                    }

                    // Only process references with valid IDs for embedding generation
                    var referencesForEmbedding = allReferences
                        .Where(r => r.Id != Guid.Empty && r.Id != default)
                        .ToList();

                    if (referencesForEmbedding.Any())
                    {
                        try
                        {
                            // Get all embeddings for references (will use cached if available)
                            var allEmbeddings = await _contentReferenceService.GetOrCreateEmbeddingsForContentAsync(
                                referencesForEmbedding, maxChunkTokens);

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
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating embeddings for context references, using fallback approach");
                            AddFallbackChunks(contextStringBuilder, referencesForEmbedding);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building context with selected references");

                    // Provide a simplified fallback approach that doesn't depend on embeddings
                    contextStringBuilder.AppendLine("Error processing references. Using direct content:");

                    // Just add the content being edited directly
                    var contentBeingEdited = allReferences.FirstOrDefault(r =>
                        r.DisplayName?.Contains("Content Being Edited") == true ||
                        r.ReferenceType == ContentReferenceType.GeneratedSection);

                    if (contentBeingEdited != null && !string.IsNullOrEmpty(contentBeingEdited.RagText))
                    {
                        contextStringBuilder.AppendLine(contentBeingEdited.RagText);
                    }
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
            // Simple fallback to just take the first 100-200 characters from each reference
            foreach (var reference in references.Take(5))
            {
                if (!string.IsNullOrEmpty(reference.RagText))
                {
                    var excerpt = reference.RagText.Length > 500
                        ? reference.RagText.Substring(0, 500) + "..."
                        : reference.RagText;

                    contextStringBuilder.AppendLine($"From {reference.DisplayName ?? "reference"}:");
                    contextStringBuilder.AppendLine(excerpt);
                    contextStringBuilder.AppendLine();
                }
            }
        }
    }
}
