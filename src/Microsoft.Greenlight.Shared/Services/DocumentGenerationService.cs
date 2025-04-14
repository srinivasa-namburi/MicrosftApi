using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service that acts as a facade for Orleans calls related to Document Generation
    /// </summary>
    public class DocumentGenerationService : IDocumentGenerationService
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<DocumentGenerationService> _logger;

        public DocumentGenerationService(
            IClusterClient clusterClient,
            ILogger<DocumentGenerationService> logger)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }
    
        public async Task<Guid> StartDocumentGenerationAsync(GenerateDocumentDTO request)
        {
            var documentId = Guid.NewGuid();
        
            try
            {
                _logger.LogInformation("Starting document generation with ID {DocumentId}", documentId);
            
                var grain = _clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
                await grain.StartDocumentGenerationAsync(request);
            
                return documentId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start document generation with ID {DocumentId}", documentId);
                throw;
            }
        }
    
        public async Task<DocumentGenerationState> GetDocumentGenerationStatusAsync(Guid documentId)
        {
            var grain = _clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            return await grain.GetStateAsync();
        }
    }

    public interface IDocumentGenerationService
    {
        Task<Guid> StartDocumentGenerationAsync(GenerateDocumentDTO request);
        Task<DocumentGenerationState> GetDocumentGenerationStatusAsync(Guid documentId);
    }
}