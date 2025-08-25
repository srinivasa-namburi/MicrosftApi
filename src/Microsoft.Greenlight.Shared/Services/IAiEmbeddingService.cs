// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service for generating and working with AI embeddings
/// </summary>
public interface IAiEmbeddingService
{
    /// <summary>
    /// Generates text embeddings from the provided text content using the globally configured embedding deployment.
    /// </summary>
    /// <param name="text">The text to generate embeddings for.</param>
    /// <returns>An array of floating point values representing the embedding vector.</returns>
    Task<float[]> GenerateEmbeddingsAsync(string text);

    /// <summary>
    /// Generates text embeddings using a specific embedding model deployment and optional dimensionality.
    /// </summary>
    /// <param name="text">Text to embed.</param>
    /// <param name="deploymentName">Embedding model deployment name.</param>
    /// <param name="dimensions">Optional target embedding dimensionality. If null, model default is used.</param>
    Task<float[]> GenerateEmbeddingsAsync(string text, string deploymentName, int? dimensions);

    /// <summary>
    /// Generates embeddings using per-document process configuration when LogicType is SemanticKernelVectorStore; otherwise falls back to global configuration.
    /// </summary>
    /// <param name="documentProcessShortName">Document process short name.</param>
    /// <param name="text">Text to embed.</param>
    Task<float[]> GenerateEmbeddingsForDocumentProcessAsync(string documentProcessShortName, string text);

    /// <summary>
    /// Generates embeddings using per-document library configuration when LogicType is SemanticKernelVectorStore; otherwise falls back to global configuration.
    /// </summary>
    /// <param name="documentLibraryShortName">Document library short name.</param>
    /// <param name="text">Text to embed.</param>
    Task<float[]> GenerateEmbeddingsForDocumentLibraryAsync(string documentLibraryShortName, string text);

    /// <summary>
    /// Resolves the effective embedding deployment name and vector dimensions for a document process.
    /// </summary>
    /// <param name="documentProcessShortName">Document process short name.</param>
    /// <returns>Tuple of deployment name and dimensions.</returns>
    Task<(string DeploymentName, int Dimensions)> ResolveEmbeddingConfigForDocumentProcessAsync(string documentProcessShortName);

    /// <summary>
    /// Resolves the effective embedding deployment name and vector dimensions for a document library.
    /// </summary>
    /// <param name="documentLibraryShortName">Document library short name.</param>
    /// <returns>Tuple of deployment name and dimensions.</returns>
    Task<(string DeploymentName, int Dimensions)> ResolveEmbeddingConfigForDocumentLibraryAsync(string documentLibraryShortName);

    /// <summary>
    /// Calculates the cosine similarity between two embedding vectors
    /// </summary>
    /// <param name="vector1">First embedding vector</param>
    /// <param name="vector2">Second embedding vector</param>
    /// <returns>A similarity score between 0 and 1</returns>
    float CalculateCosineSimilarity(float[] vector1, float[] vector2);
}