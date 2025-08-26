// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Exception thrown by IAiEmbeddingService when the input exceeds the model's context length.
/// Signals callers (ingestion pipeline) to re-chunk the content using the configured ITextChunkingService
/// to preserve per-chunk embeddings and context.
/// </summary>
[Serializable]
public sealed class EmbeddingInputTooLargeException : Exception
{
    public int InputLength { get; }
    public string DeploymentName { get; }

    public EmbeddingInputTooLargeException(int inputLength, string deploymentName)
        : base($"Embedding input too large for deployment '{deploymentName}'. Length={inputLength}.")
    {
        InputLength = inputLength;
        DeploymentName = deploymentName;
    }

    public EmbeddingInputTooLargeException(int inputLength, string deploymentName, Exception inner)
        : base($"Embedding input too large for deployment '{deploymentName}'. Length={inputLength}.", inner)
    {
        InputLength = inputLength;
        DeploymentName = deploymentName;
    }

    private EmbeddingInputTooLargeException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        InputLength = info.GetInt32(nameof(InputLength));
        DeploymentName = info.GetString(nameof(DeploymentName)) ?? string.Empty;
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(InputLength), InputLength);
        info.AddValue(nameof(DeploymentName), DeploymentName);
    }
}
