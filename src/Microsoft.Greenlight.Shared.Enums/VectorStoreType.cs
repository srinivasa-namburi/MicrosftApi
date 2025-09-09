// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Types of supported vector stores.
    /// </summary>
    public enum VectorStoreType
    {
        /// <summary>
        /// PostgreSQL with pgvector extension.
        /// </summary>
        PostgreSQL,

        /// <summary>
        /// Azure AI Search vector store.
        /// </summary>
        AzureAISearch,

        /// <summary>
        /// In-memory vector store (for testing).
        /// </summary>
        InMemory
    }
}