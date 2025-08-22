// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Services.Search.Internal;

internal sealed class VectorStoreException : Exception
{
    public VectorStoreException(VectorStoreErrorReason reason, string message, Exception? inner = null) : base(message, inner) => Reason = reason;
    public VectorStoreErrorReason Reason { get; }
}

internal enum VectorStoreErrorReason
{
    Unknown,
    ProviderUnavailable,
    InvalidFilter,
    Timeout,
    EmbeddingDimensionMismatch
}
