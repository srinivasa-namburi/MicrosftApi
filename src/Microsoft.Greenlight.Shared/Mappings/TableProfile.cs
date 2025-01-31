using System.Drawing;
using AutoMapper;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Greenlight.Shared.Models;
using BoundingRegion = Azure.AI.FormRecognizer.DocumentAnalysis.BoundingRegion;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping table-related entities.
/// </summary>
public class TableProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableProfile"/> class.
    /// Defines the mapping between <see cref="DocumentTable"/> and <see cref="Table"/>, 
    /// between <see cref="DocumentTableCell"/> and <see cref="TableCell"/>,
    /// and between <see cref="BoundingRegion"/> and <see cref="Models.BoundingRegion"/>.
    /// </summary>
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
