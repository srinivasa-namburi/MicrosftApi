// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Specifies the type of document processing logic.
/// </summary>
public enum DocumentProcessLogicType
{
    /// <summary>
    /// Processing logic that uses kernel memory.
    /// </summary>
    KernelMemory = 100,

    /// <summary>
    /// Classic processing logic.
    /// </summary>
    Classic = 200,

    /// <summary>
    /// Processing logic that uses Semantic Kernel Vector Store.
    /// </summary>
    SemanticKernelVectorStore = 300
}
