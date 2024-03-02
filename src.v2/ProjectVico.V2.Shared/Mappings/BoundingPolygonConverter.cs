using System.Drawing;
using AutoMapper;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Mappings;

public class BoundingPolygonConverter : ITypeConverter<PointF, BoundingPolygon>
{
    public BoundingPolygon Convert(PointF source, BoundingPolygon destination, ResolutionContext context)
    {
        var boundingPolygon = new BoundingPolygon()
        {
            Id = Guid.NewGuid(),
            X = new decimal(source.X),
            Y = new decimal(source.Y)

        };

        boundingPolygon.IsEmpty = !(boundingPolygon.X != 0 || boundingPolygon.Y != 0);
        return boundingPolygon;
    }
}