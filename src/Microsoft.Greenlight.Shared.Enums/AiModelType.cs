// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Defines the types of AI models that can be configured and deployed.
    /// </summary>
    public enum AiModelType
    {
        /// <summary>
        /// Standard chat and completion models (e.g., GPT-4o, GPT-3.5).
        /// </summary>
        Chat = 0,

        /// <summary>
        /// Embedding models for generating vector embeddings (e.g., text-embedding-ada-002, text-embedding-3-small).
        /// </summary>
        Embedding = 1
    }
}