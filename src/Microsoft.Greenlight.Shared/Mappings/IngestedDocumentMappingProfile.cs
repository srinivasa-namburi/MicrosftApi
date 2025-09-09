// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.FileStorage;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// AutoMapper profile for mapping IngestedDocument model to IngestedDocumentInfo DTO.
/// Injects IFileUrlResolverService via a resolver to populate ResolvedUrl consistently.
/// </summary>
public sealed class IngestedDocumentMappingProfile : Profile
{
    public IngestedDocumentMappingProfile()
    {
        CreateMap<IngestedDocument, IngestedDocumentInfo>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.FileName, opt => opt.MapFrom(s => s.FileName))
            .ForMember(d => d.FileHash, opt => opt.MapFrom(s => s.FileHash))
            .ForMember(d => d.OriginalDocumentUrl, opt => opt.MapFrom(s => s.OriginalDocumentUrl ?? string.Empty))
            .ForMember(d => d.FinalBlobUrl, opt => opt.MapFrom(s => s.FinalBlobUrl ?? string.Empty))
            .ForMember(d => d.UploadedByUserOid, opt => opt.MapFrom(s => s.UploadedByUserOid))
            .ForMember(d => d.DocumentLibraryOrProcessName, opt => opt.MapFrom(s => s.DocumentLibraryOrProcessName))
            .ForMember(d => d.DocumentLibraryType, opt => opt.MapFrom(s => s.DocumentLibraryType))
            .ForMember(d => d.IngestionState, opt => opt.MapFrom(s => s.IngestionState))
            .ForMember(d => d.IngestedDate, opt => opt.MapFrom(s => s.IngestedDate))
            .ForMember(d => d.Container, opt => opt.MapFrom(s => s.Container))
            .ForMember(d => d.FolderPath, opt => opt.MapFrom(s => s.FolderPath))
            .ForMember(d => d.OrchestrationId, opt => opt.MapFrom(s => s.OrchestrationId))
            .ForMember(d => d.Error, opt => opt.MapFrom(s => s.Error))
            .ForMember(d => d.CreatedUtc, opt => opt.MapFrom(s => s.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, opt => opt.MapFrom(s => s.ModifiedUtc))
            // Populate ResolvedUrl through a DI-backed resolver. This keeps all URL exposure centralized.
            .ForMember(d => d.ResolvedUrl, opt => opt.MapFrom<FileUrlResolvedUrlValueResolver>());
    }
}

/// <summary>
/// Resolves a proxied URL for an ingested document via IFileUrlResolverService.
/// AutoMapper executes resolvers synchronously; we block on the async call.
/// </summary>
public sealed class FileUrlResolvedUrlValueResolver : IValueResolver<IngestedDocument, IngestedDocumentInfo, string?>
{
    private readonly IFileUrlResolverService _resolver;

    public FileUrlResolvedUrlValueResolver(IFileUrlResolverService resolver)
    {
        _resolver = resolver;
    }

    public string? Resolve(IngestedDocument source, IngestedDocumentInfo destination, string? destMember, ResolutionContext context)
    {
        try
        {
            // Prefer lookup by document id; resolver handles FileStorageSource and legacy blob scenarios.
            return _resolver.ResolveUrlByDocumentIdAsync(source.Id).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }
}

