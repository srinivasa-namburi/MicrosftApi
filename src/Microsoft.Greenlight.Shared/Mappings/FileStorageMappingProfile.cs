// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Enums; // Added for FileStorageSourceDataType enum

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// AutoMapper profile for mapping between file storage models and DTOs.
/// </summary>
public class FileStorageMappingProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageMappingProfile"/> class.
    /// </summary>
    public FileStorageMappingProfile()
    {
        // FileStorageHost mappings
        CreateMap<FileStorageHost, FileStorageHostInfo>()
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.LastUpdatedDate, opt => opt.MapFrom(src => src.ModifiedUtc));

        CreateMap<FileStorageHostInfo, FileStorageHost>()
            .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreatedDate))
            .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => src.LastUpdatedDate))
            .ForMember(dest => dest.Sources, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        // FileStorageSource mappings
        CreateMap<FileStorageSource, FileStorageSourceInfo>()
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.LastUpdatedDate, opt => opt.MapFrom(src => src.ModifiedUtc))
            .ForMember(dest => dest.StorageSourceDataType, opt => opt.MapFrom(src => src.StorageSourceDataType))
            .ForMember(dest => dest.StorageSourceDataTypes, opt => opt.MapFrom(src => src.Categories.Select(c => c.DataType).ToList()));

        CreateMap<FileStorageSourceInfo, FileStorageSource>()
            .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreatedDate))
            .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => src.LastUpdatedDate))
            .ForMember(dest => dest.StorageSourceDataType, opt => opt.MapFrom(src => src.StorageSourceDataType))
            .ForMember(dest => dest.FileStorageHost, opt => opt.Ignore())
            .ForMember(dest => dest.DocumentProcessSources, opt => opt.Ignore())
            .ForMember(dest => dest.DocumentLibrarySources, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
            .AfterMap((src, dest) =>
            {
                // Ensure collection
                dest.Categories ??= new List<FileStorageSourceCategory>();

                // Desired list (handles null by coalescing to empty list first)
                var desired = (src.StorageSourceDataTypes ?? new List<FileStorageSourceDataType>())
                    .Distinct()
                    .ToList();

                // Remove categories not in desired list
                var toRemove = dest.Categories.Where(c => !desired.Contains(c.DataType)).ToList();
                foreach (var rem in toRemove)
                {
                    dest.Categories.Remove(rem);
                }

                // Add any missing ones
                foreach (var dt in desired)
                {
                    if (!dest.Categories.Any(c => c.DataType == dt))
                    {
                        dest.Categories.Add(new FileStorageSourceCategory
                        {
                            Id = Guid.NewGuid(),
                            FileStorageSourceId = dest.Id,
                            DataType = dt
                        });
                    }
                }
            });

        CreateMap<DocumentProcessFileStorageSource, DocumentProcessFileStorageSourceInfo>()
            .ForMember(dest => dest.FileStorageSourceName, opt => opt.MapFrom(src => src.FileStorageSource != null ? src.FileStorageSource.Name : string.Empty))
            .ForMember(dest => dest.DocumentProcessName, opt => opt.MapFrom(src => src.DocumentProcess != null ? src.DocumentProcess.ShortName : string.Empty))
            .ForMember(dest => dest.ProviderType, opt => opt.MapFrom(src => src.FileStorageSource != null && src.FileStorageSource.FileStorageHost != null ? src.FileStorageSource.FileStorageHost.ProviderType : default))
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.LastUpdatedDate, opt => opt.MapFrom(src => src.ModifiedUtc));

        CreateMap<DocumentLibraryFileStorageSource, DocumentLibraryFileStorageSourceInfo>()
            .ForMember(dest => dest.FileStorageSourceName, opt => opt.MapFrom(src => src.FileStorageSource != null ? src.FileStorageSource.Name : string.Empty))
            .ForMember(dest => dest.DocumentLibraryName, opt => opt.MapFrom(src => src.DocumentLibrary != null ? src.DocumentLibrary.ShortName : string.Empty))
            .ForMember(dest => dest.ProviderType, opt => opt.MapFrom(src => src.FileStorageSource != null && src.FileStorageSource.FileStorageHost != null ? src.FileStorageSource.FileStorageHost.ProviderType : default))
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.LastUpdatedDate, opt => opt.MapFrom(src => src.ModifiedUtc));

        CreateMap<FileAcknowledgmentRecord, FileAcknowledgmentRecordInfo>()
            .ForMember(dest => dest.FileStorageSourceName, opt => opt.MapFrom(src => src.FileStorageSource != null ? src.FileStorageSource.Name : string.Empty))
            .ForMember(dest => dest.ProviderType, opt => opt.MapFrom(src => src.FileStorageSource != null && src.FileStorageSource.FileStorageHost != null ? src.FileStorageSource.FileStorageHost.ProviderType : default))
            .ForMember(dest => dest.AcknowledgedDate, opt => opt.MapFrom(src => src.CreatedUtc));
    }
}
