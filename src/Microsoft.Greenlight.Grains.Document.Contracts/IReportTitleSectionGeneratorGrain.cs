using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IReportTitleSectionGeneratorGrain : IGrainWithGuidKey
    {
        Task GenerateSectionAsync(Guid documentId, string? authorOid, string contentNodeJson, 
            string documentOutlineJson, Guid? metadataId);
    }
}