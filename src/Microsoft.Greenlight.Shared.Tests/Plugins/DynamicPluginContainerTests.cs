using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Plugins;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Plugins
{
    public class DynamicPluginContainerTests
    {
        private const string TEST_PLUGIN = "TestPlugin";
        private const string VERSION = "1.0.0";
        private const string TEST_PLUGIN_1 = "TestPlugin1";
        private const string TEST_PLUGIN_2 = "TestPlugin2";

        [Fact]
        public void AddPlugin_ShouldAddPluginCorrectly()
        {
            // Arrange
            var container = new DynamicPluginContainer();
            var pluginInfo = new LoadedDynamicPluginInfo
            {
                Plugin = new DynamicPlugin { Name = TEST_PLUGIN, BlobContainerName = "TestContainer" },
                Version = new DynamicPluginVersion(1, 0, 0),
                Assembly = typeof(DynamicPluginContainer).Assembly,
                PluginTypes = [typeof(DynamicPlugin)],
                TempDirectory = "TempDir"
            };

            // Act
            container.AddPlugin(TEST_PLUGIN, VERSION, pluginInfo);

            // Assert
            Assert.True(container.TryGetPlugin(TEST_PLUGIN, VERSION, out var retrievedPluginInfo));
            Assert.Equal(pluginInfo, retrievedPluginInfo);
        }

        [Fact]
        public void TryGetPlugin_ShouldReturnFalseIfPluginNotFound()
        {
            // Arrange
            var container = new DynamicPluginContainer();

            // Act
            var result = container.TryGetPlugin("NonExistentPlugin", VERSION, out var pluginInfo);

            // Assert
            Assert.False(result);
            Assert.Null(pluginInfo);
        }

        
        [Fact]
        public void GetAllPlugins_ShouldReturnAllAddedPlugins()
        {
            // Arrange
            var container = new DynamicPluginContainer();
            var pluginInfo1 = new LoadedDynamicPluginInfo
            {
                Plugin = new DynamicPlugin { Name = TEST_PLUGIN_1, BlobContainerName = "TestContainer1" },
                Version = new DynamicPluginVersion(1, 0, 0),
                Assembly = typeof(DynamicPluginContainer).Assembly,
                PluginTypes = [typeof(DynamicPlugin)],
                TempDirectory = "TempDir1"
            };
            var pluginInfo2 = new LoadedDynamicPluginInfo
            {
                Plugin = new DynamicPlugin { Name = TEST_PLUGIN_2, BlobContainerName = "TestContainer2" },
                Version = new DynamicPluginVersion(1, 0, 0),
                Assembly = typeof(DynamicPluginContainer).Assembly,
                PluginTypes = [typeof(DynamicPlugin)],
                TempDirectory = "TempDir2"
            };

            container.AddPlugin(TEST_PLUGIN_1, VERSION, pluginInfo1);
            container.AddPlugin(TEST_PLUGIN_2, VERSION, pluginInfo2);

            // Act
            var allPlugins = container.GetAllPlugins().ToList();

            // Assert the conversion from a dictionary to a list was successful
            Assert.Contains(pluginInfo1, allPlugins);
            Assert.Contains(pluginInfo2, allPlugins);
        }
    }
}
