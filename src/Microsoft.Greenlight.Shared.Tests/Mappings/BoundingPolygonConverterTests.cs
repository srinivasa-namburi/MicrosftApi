using System.Drawing;
using AutoMapper;
using Microsoft.Greenlight.Shared.Models;
using Xunit;
using Assert = Xunit.Assert;
using Microsoft.Greenlight.Shared.Mappings;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class BoundingPolygonConverterTests
    {
        private readonly IMapper _mapper;

        public BoundingPolygonConverterTests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<PointF, BoundingPolygon>().ConvertUsing<BoundingPolygonConverter>();
            });
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Convert_PointFToBoundingPolygon_ReturnsExpectedResult()
        {
            // Arrange
            var point = new PointF(10.5f, 20.5f);

            // Act
            var result = _mapper.Map<BoundingPolygon>(point);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(new decimal(point.X), result.X);
            Assert.Equal(new decimal(point.Y), result.Y);
            Assert.False(result.IsEmpty);
        }

        [Fact]
        public void Convert_PointFWithZeroCoordinates_ReturnsEmptyBoundingPolygon()
        {
            // Arrange
            var point = new PointF(0f, 0f);

            // Act
            var result = _mapper.Map<BoundingPolygon>(point);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.X);
            Assert.Equal(0, result.Y);
            Assert.True(result.IsEmpty);
        }
    }
}
