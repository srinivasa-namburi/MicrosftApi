using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Mappings;
using Xunit;
using Assert = Xunit.Assert;


namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class PluginMappingProfileTests
    {
        private readonly IMapper _mapper;

        public PluginMappingProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<PluginMappingProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_DynamicPlugin_To_DynamicPluginInfo()
        {
            // Arrange
            var dynamicPlugin = new DynamicPlugin
            {
                Id = Guid.NewGuid(),
                Name = "Test Plugin",
                BlobContainerName = "test-plugin",
                Versions = [new DynamicPluginVersion(1, 0 , 0)]
            };

            // Act
            var dynamicPluginInfo = _mapper.Map<DynamicPluginInfo>(dynamicPlugin);

            // Assert
            Assert.Equal(dynamicPlugin.LatestVersion!.ToString(), dynamicPluginInfo.LatestVersion.ToString());
        }
    }
}
