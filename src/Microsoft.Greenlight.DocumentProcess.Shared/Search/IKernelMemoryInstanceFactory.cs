using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public interface IKernelMemoryInstanceFactory
{
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(string documentLibraryShortName);
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(Guid documentLibraryId);
}