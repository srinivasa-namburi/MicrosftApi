using AutoMapper;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Mappings;

public class DocumentInfoProfile : Profile
{
    public DocumentInfoProfile()
    {
        
        CreateMap<DocumentProcessOptions, DocumentProcessInfo>()
            .ForMember(x => x.ShortName, y => y.MapFrom(source => source.Name))
            .ForMember(x => x.Description, y => y.Ignore())
            .ForMember(x => x.Repositories, y => y.MapFrom(source => source.Repositories))
            // For the LogicType, we need to map the string value from the source IngestionMethod property to the DocumentProcessLogicType enum
            // If it's null or empty, we default to KernelMemory
            .ForMember(x => x.LogicType, y => y.MapFrom(source => Enum.Parse<DocumentProcessLogicType>(source.IngestionMethod ?? "KernelMemory")))
            .ForMember(x => x.BlobStorageContainerName, y => y.MapFrom(source => source.BlobStorageContainerName))
            .ForMember(x => x.BlobStorageAutoImportFolderName, y => y.MapFrom(source => source.BlobStorageAutoImportFolderName))
            .ForMember(x => x.ClassifyDocuments, y => y.MapFrom(source => source.ClassifyDocuments))
            .ForMember(x => x.ClassificationModelName, y => y.MapFrom(source => source.ClassificationModelName));

        // All the properties are the same, so we can just map the source to the destination
        // DynamicDocumentProcessDefinition has additional properties, but they are not used in the mapping
        CreateMap<DynamicDocumentProcessDefinition, DocumentProcessInfo>();
    }
}