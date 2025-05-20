// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    /// <summary>
    /// Orleans grain interface for managing shared content state for a document section.
    /// </summary>
    public interface IContentStateGrain : IGrainWithGuidKey
    {
        Task SetSourceDocumentsAsync(string sourceDocuments, int blockSize);
        Task<string> GetSourceDocumentsAsync();
        Task StoreSequenceContentAsync(int sequenceNumber, string content);
        Task<string> GetSequenceContentAsync(int sequenceNumber);
        Task RemoveSequenceContentAsync(int sequenceNumber);
        Task<string> GetAssembledContentAsync();
        Task<string> GetSequenceNumbersAsync();
        Task<string> GetNextSequenceNumberAsync();
        Task<string> GetSequenceWithContextAsync(int sequenceNumber);
        Task ClearAndDeactivateAsync();
    }
}
