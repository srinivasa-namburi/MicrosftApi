// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Services;
using OpenAI.Embeddings;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.Services;

/// <summary>
/// Unit tests for verifying that dimensions are omitted for models that don't support it (e.g., text-embedding-ada-002).
/// </summary>
public class AiEmbeddingServiceDimensionOmissionTests
{
    [Fact]
    public void PrepareEmbeddingOptions_OmitsDimensions_ForAda002()
    {
        var (options, attempted, omit) = AiEmbeddingService.PrepareEmbeddingOptions("my-text-embedding-ada-002-deployment", 512);
        Assert.True(omit);
        Assert.False(attempted);
        Assert.Null(options.Dimensions);
    }

    [Fact]
    public void PrepareEmbeddingOptions_SetsDimensions_ForNonAdaModels()
    {
        var (options, attempted, omit) = AiEmbeddingService.PrepareEmbeddingOptions("text-embedding-3-small", 512);
        Assert.False(omit);
        Assert.True(attempted);
        Assert.Equal(512, options.Dimensions);
    }

    [Fact]
    public void PrepareEmbeddingOptions_NoDimensions_WhenNotProvided()
    {
        var (options, attempted, omit) = AiEmbeddingService.PrepareEmbeddingOptions("text-embedding-3-small", null);
        Assert.False(omit);
        Assert.False(attempted);
        Assert.Null(options.Dimensions);
    }
}

