// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Abstraction for generating vector embeddings for text.
/// Implementations can wrap Semantic Kernel embedding generators or custom services.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Generates an embedding vector for the supplied text.
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedding vector.</returns>
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
