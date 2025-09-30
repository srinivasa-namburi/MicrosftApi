// Copyright (c) Microsoft Corporation. All rights reserved.

using Orleans;

public interface IDocumentProcessMetadataReindexingGrain : IGrainWithGuidKey
{
    Task ExecuteAsync();
}