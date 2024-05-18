using System.Drawing;
using AutoMapper;
using Azure.AI.FormRecognizer.DocumentAnalysis;
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