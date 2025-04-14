using Orleans;

namespace Microsoft.Greenlight.Grains.Document.Contracts
{
    public interface IDocumentOutlineGeneratorGrain : IGrainWithGuidKey
    {
        Task GenerateOutlineAsync(Guid documentId, string documentTitle, string authorOid, string documentProcessName);
    }
}