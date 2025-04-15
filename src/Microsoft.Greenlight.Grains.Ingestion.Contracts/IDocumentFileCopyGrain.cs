using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    public interface IDocumentFileCopyGrain : IGrainWithGuidKey
    {
        Task CopyFilesFromBlobStorageAsync(
            string sourceContainerName, 
            string sourceFolderPath,
            string targetContainerName,
            string documentLibraryShortName,
            DocumentLibraryType documentLibraryType);
    }
}