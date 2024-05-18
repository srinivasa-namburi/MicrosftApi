using System.Drawing;
using AutoMapper;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;
using BoundingRegion = Azure.AI.FormRecognizer.DocumentAnalysis.BoundingRegion;

namespace ProjectVico.V2.Shared.Mappings;

public class TableProfile : Profile
{
    public TableProfile()
    {
        CreateMap<PointF, BoundingPolygon>().ConvertUsing<BoundingPolygonConverter>();

        CreateMap<DocumentTable, Table>();
        CreateMap<DocumentTableCell, TableCell>()
            .ForMember(x => x.Text, y => y.MapFrom(source => source.Content))
            .ForMember(x => x.RowSpan, y => y.MapFrom(source => source.RowSpan))
            .ForMember(x => x.ColumnSpan, y => y.MapFrom(source => source.ColumnSpan));

        CreateMap<BoundingRegion, Models.BoundingRegion>()
            .ForMember(x => x.Page, y => y.MapFrom(source => source.PageNumber))
            .ForMember(x => x.BoundingPolygons, y => y.MapFrom(source => source.BoundingPolygon));
    }
}

public class DocumentProcessDefinitionProfile : Profile
{
    public DocumentProcessDefinitionProfile()
    {
        
        CreateMap<DocumentProcessOptions, DocumentProcessDefinition>()
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
    }
}