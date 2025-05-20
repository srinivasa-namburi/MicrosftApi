// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Grains.Document.Contracts;
using System.ComponentModel;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;

public class ContentStatePlugin
{
    private readonly IGrainFactory _grainFactory;
    private readonly string _sourceDocuments;
    private readonly int _blockSize;
    private readonly Guid _executionId;
    private bool _initialized = false;

    public ContentStatePlugin(IGrainFactory grainFactory, Guid executionId, string sourceDocuments, int blockSize)
    {
        _grainFactory = grainFactory;
        _executionId = executionId;
        _sourceDocuments = sourceDocuments;
        _blockSize = blockSize;
    }

    /// <summary>
    /// Initializes the grain state. Must be called before using the plugin.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
            await grain.SetSourceDocumentsAsync(_sourceDocuments, _blockSize);
            _initialized = true;
        }
    }

    [KernelFunction, Description("Get the content for a specific sequence number")]
    public async Task<string> GetSequenceContent(int sequenceNumber)
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetSequenceContentAsync(sequenceNumber);
    }

    [KernelFunction, Description("Get all content assembled in order")]
    public async Task<string> GetAssembledContent()
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetAssembledContentAsync();
    }

    [KernelFunction, Description("Store content for a specific sequence")]
    public async Task StoreSequenceContent(int sequenceNumber, string content)
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        await grain.StoreSequenceContentAsync(sequenceNumber, content);
    }

    [KernelFunction, Description("Remove a specific sequence")]
    public async Task RemoveSequenceContent(int sequenceNumber)
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        await grain.RemoveSequenceContentAsync(sequenceNumber);
    }

    [KernelFunction, Description("Get all source documents")]
    public async Task<string> GetSourceDocuments()
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetSourceDocumentsAsync();
    }

    [KernelFunction, Description("Get all sequence numbers in use")]
    public async Task<string> GetSequenceNumbers()
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetSequenceNumbersAsync();
    }

    [KernelFunction, Description("Gets the next available sequence number for producing content output")]
    public async Task<string> GetNextSequenceNumber()
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetNextSequenceNumberAsync();
    }

    [KernelFunction, Description("Get content with surrounding context")]
    public async Task<string> GetSequenceWithContext(int sequenceNumber)
    {
        var grain = _grainFactory.GetGrain<IContentStateGrain>(_executionId);
        return await grain.GetSequenceWithContextAsync(sequenceNumber);
    }
}