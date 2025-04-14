using Microsoft.Greenlight.Shared.Contracts.DTO;
using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IDocumentCreatorGrain : IGrainWithGuidKey
    {
        Task CreateDocumentAsync(Guid documentId, GenerateDocumentDTO request);
    }
}