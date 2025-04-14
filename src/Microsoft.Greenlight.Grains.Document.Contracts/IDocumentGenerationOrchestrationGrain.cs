using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IDocumentGenerationOrchestrationGrain : IGrainWithGuidKey
    {
        Task<DocumentGenerationState> GetStateAsync();
        Task StartDocumentGenerationAsync(GenerateDocumentDTO request);
        Task OnDocumentCreatedAsync(Guid metadataId);
        Task OnDocumentOutlineGeneratedAsync(string generatedDocumentJson);
        Task OnDocumentOutlineGenerationFailedAsync();
        Task OnReportContentGenerationSubmittedAsync(int numberOfContentNodesToGenerate);
        Task OnContentNodeGeneratedAsync(Guid contentNodeId, bool isSuccessful);
        Task OnContentNodeStateChangedAsync(Guid contentNodeId, ContentNodeGenerationState state);
    }
}