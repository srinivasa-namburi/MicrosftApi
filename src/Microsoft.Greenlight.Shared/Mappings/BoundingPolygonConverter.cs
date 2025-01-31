using System.Drawing;
using AutoMapper;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Converts a <see cref="PointF"/> to a BoundingPolygon.
/// </summary>
public class BoundingPolygonConverter : ITypeConverter<PointF, BoundingPolygon>
{
    /// <summary>
    /// Converts a <see cref="PointF"/> to a BoundingPolygon.
    /// </summary>
    /// <param name="source">The source <see cref="PointF"/>.</param>
    /// <param name="destination">Not used by this function.</param>
    /// <param name="context">Not used by this function.</param>
    /// <returns>A new BoundingPolygon instance.</returns>
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
