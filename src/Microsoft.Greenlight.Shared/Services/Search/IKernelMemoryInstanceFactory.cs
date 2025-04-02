using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Services.Search;

public interface IKernelMemoryInstanceFactory
{
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(string documentLibraryShortName);
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(Guid documentLibraryId);
    IKernelMemory GetKernelMemoryForAdhocUploads();
}