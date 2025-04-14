using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Services.Search;

public interface IKernelMemoryInstanceFactory
{
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(string documentLibraryShortName);
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(Guid documentLibraryId);
    IKernelMemory GetKernelMemoryForAdhocUploads();
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentProcess(string documentProcessShortName);
    Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(DocumentLibraryInfo documentLibraryInfo);
}