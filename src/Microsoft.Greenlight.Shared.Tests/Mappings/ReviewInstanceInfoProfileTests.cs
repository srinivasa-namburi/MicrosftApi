using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;
using Xunit;
using Assert = Xunit.Assert;
using Microsoft.Greenlight.Shared.Mappings;
using System.Text.Json;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class ReviewInstanceInfoProfileTests
    {
        private readonly IMapper _mapper;

        public ReviewInstanceInfoProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<ReviewInstanceInfoProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_ReviewInstance_To_ReviewInstanceInfo()
        {
            // Arrange
            var reviewInstance = new ReviewInstance
            {
                ReviewDefinitionId = Guid.NewGuid(),
                ExternalLinkAssetId = Guid.NewGuid(),
                ReviewDefinition = new ReviewDefinition
                {
                    Title = "Test Review",
                    Description = "Test Description"
                }
            };

            // Act
            var reviewInstanceInfo = _mapper.Map<ReviewInstanceInfo>(reviewInstance);

            // Assert
            Assert.NotNull(reviewInstanceInfo);
            Assert.Equal(JsonSerializer.Serialize(reviewInstance.ReviewDefinition), reviewInstanceInfo.ReviewDefinitionStateWhenSubmitted);
        }
    }
}
