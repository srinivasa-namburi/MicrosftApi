using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public class KernelMemoryInstanceFactory : IKernelMemoryInstanceFactory
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IServiceProvider _sp;
    private readonly KernelMemoryInstanceContainer _instanceContainer;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public KernelMemoryInstanceFactory(
        DocGenerationDbContext dbContext,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IServiceProvider sp,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        KernelMemoryInstanceContainer instanceContainer)
    {
        _dbContext = dbContext;
        _documentLibraryInfoService = documentLibraryInfoService;
        _sp = sp;
        _instanceContainer = instanceContainer;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public async Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(string documentLibraryShortName)
    {

        if (_instanceContainer.KernelMemoryInstances.TryGetValue(documentLibraryShortName, out var memory))
        {
            return memory;
        }

        var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
        if (documentLibrary == null)
        {
            throw new Exception($"Document Library with short name {documentLibraryShortName} not found.");
        }

        return await GetKernelMemoryInstanceForDocumentLibrary(documentLibrary);
    }

    public async Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(Guid documentLibraryId)
    {
        var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByIdAsync(documentLibraryId);
        if (documentLibrary == null)
        {
            throw new Exception($"Document Library with id {documentLibraryId} not found.");
        }

        return await GetKernelMemoryInstanceForDocumentLibrary(documentLibrary);
    }

    private async Task<IKernelMemory> GetKernelMemoryInstanceForDocumentLibrary(DocumentLibraryInfo documentLibraryInfo)
    {
        var documentLibraryShortName = documentLibraryInfo.ShortName;
        if (_instanceContainer.KernelMemoryInstances.TryGetValue(documentLibraryInfo.ShortName, out var library))
        {
            return library;
        }
        else
        {
            if (documentLibraryInfo == null)
            {
                throw new Exception($"Document Library with short name {documentLibraryShortName} not found.");
            }

            var kernelMemory = _sp.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);

            _instanceContainer.KernelMemoryInstances[documentLibraryShortName] = kernelMemory;

            return kernelMemory;
        }
    }
}