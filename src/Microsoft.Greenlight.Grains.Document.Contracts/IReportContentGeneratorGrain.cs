using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IReportContentGeneratorGrain : IGrainWithGuidKey
    {
        [ResponseTimeout("04:00:00")] // May block while scheduling many sections; allow up to 4 hours
        Task GenerateContentAsync(Guid documentId, string? authorOid, string generatedDocumentJson, 
            string documentProcessName, Guid? metadataId);
    }
}