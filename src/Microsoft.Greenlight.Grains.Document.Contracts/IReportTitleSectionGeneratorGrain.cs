using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IReportTitleSectionGeneratorGrain : IGrainWithGuidKey
    {
        [ResponseTimeout("04:00:00")] // Section might queue on the GlobalConcurrencyCoordinator and run long
        Task GenerateSectionAsync(Guid documentId, string? authorOid, string contentNodeJson, 
            string documentOutlineJson, Guid? metadataId);
    }
}