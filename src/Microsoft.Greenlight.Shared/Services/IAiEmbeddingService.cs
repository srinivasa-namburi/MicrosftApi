namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service for generating and working with AI embeddings
/// </summary>
public interface IAiEmbeddingService
{
    /// <summary>
    /// Generates text embeddings from the provided text content
    /// </summary>
    /// <param name="text">The text to generate embeddings for</param>
    /// <returns>An array of floating point values representing the embedding vector</returns>
    Task<float[]> GenerateEmbeddingsAsync(string text);
    
    /// <summary>
    /// Calculates the cosine similarity between two embedding vectors
    /// </summary>
    /// <param name="vector1">First embedding vector</param>
    /// <param name="vector2">Second embedding vector</param>
    /// <returns>A similarity score between 0 and 1</returns>
    float CalculateCosineSimilarity(float[] vector1, float[] vector2);
}