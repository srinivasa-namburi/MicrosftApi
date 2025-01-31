using System.Reflection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Plugins;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Plugins
{
    public class LoadedDynamicPluginInfoTests
    {
        [Fact]
        public void CreateFrom_ValidParameters_ReturnsLoadedDynamicPluginInfo()
        {
            // Arrange
            var plugin = new DynamicPlugin { Name = "TestPlugin", BlobContainerName = "TestContainer" };
            var version = new DynamicPluginVersion(1, 0, 0);
            var tempDirectory = "C:\\Temp";
            var assembly = Assembly.GetExecutingAssembly();

            // Act
            var result = LoadedDynamicPluginInfo.CreateFrom(plugin, version, tempDirectory, assembly);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(plugin, result.Plugin);
            Assert.Equal(version, result.Version);
            Assert.Equal(tempDirectory, result.TempDirectory);
            Assert.Equal(assembly, result.Assembly);
            Assert.NotEmpty(result.PluginTypes);
        }

        [Fact]
        public void CreateFrom_NoPluginTypesFound_ReturnsLoadedDynamicPluginInfoWithEmptyPluginTypes()
        {
            // Arrange
            var plugin = new DynamicPlugin { Name = "TestPlugin", BlobContainerName = "TestContainer" };
            var version = new DynamicPluginVersion(1, 0, 0);
            var tempDirectory = "C:\\Temp";
            var assembly = Assembly.Load("System.Runtime");

            // Act
            var result = LoadedDynamicPluginInfo.CreateFrom(plugin, version, tempDirectory, assembly);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(plugin, result.Plugin);
            Assert.Equal(version, result.Version);
            Assert.Equal(tempDirectory, result.TempDirectory);
            Assert.Equal(assembly, result.Assembly);
            Assert.Empty(result.PluginTypes);
        }

        public class TestPlugin : IPluginImplementation
        {

        }
    }
}
