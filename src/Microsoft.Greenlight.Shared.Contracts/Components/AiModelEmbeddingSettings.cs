// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.Components
{
    /// <summary>
    /// Settings specific to embedding models, including dimensions and content length limits.
    /// </summary>
    public class AiModelEmbeddingSettings
    {
        /// <summary>
        /// Number of dimensions for the embedding vectors. Valid values: 256, 512, 1024, 1536, 3072.
        /// Defaults to 1536 if not specified.
        /// </summary>
        public int Dimensions { get; set; } = 1536;

        /// <summary>
        /// Maximum content length in characters that can be processed by the embedding model.
        /// Defaults to 8192 characters.
        /// </summary>
        public int MaxContentLength { get; set; } = 8192;
    }
}