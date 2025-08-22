// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Indicates which text chunking strategy to use for vector store ingestion.
/// </summary>
public enum TextChunkingMode
{
    /// <summary>
    /// Basic token-size / sentence based chunking.
    /// </summary>
    Simple = 0,

    /// <summary>
    /// Semantic / structure-aware chunking.
    /// </summary>
    Semantic = 1
}
