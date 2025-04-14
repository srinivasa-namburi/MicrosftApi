using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IReportContentGeneratorGrain : IGrainWithGuidKey
    {
        Task GenerateContentAsync(Guid documentId, string? authorOid, string generatedDocumentJson, 
            string documentProcessName, Guid? metadataId);
    }
}