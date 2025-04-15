using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    public interface IDocumentProcessorGrain : IGrainWithGuidKey
    {
        Task ProcessDocumentAsync(
            string fileName,
            string documentUrl,
            string documentLibraryShortName,
            DocumentLibraryType documentLibraryType,
            Guid orchestrationGrainId,
            string? uploadedByUserOid = null);
    }
}