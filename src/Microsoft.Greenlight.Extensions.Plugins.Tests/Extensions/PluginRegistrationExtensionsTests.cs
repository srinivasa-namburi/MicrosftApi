using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins.Extensions;

namespace Microsoft.Greenlight.Extensions.Plugins.Tests.Extensions
{
    public class PluginRegistrationExtensionsTests
    {
        [Fact]
        public void AddScopedKeyedPlugin_ShouldAddScopedService()
        {
            // Arrange
            var services = new ServiceCollection();
            var pluginType = typeof(MockPlugin);

            // Act
            services.AddScopedKeyedPlugin(pluginType);

            // Assert
            var serviceDescriptor = Assert.Single(services);
            Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);
            Assert.Equal(pluginType, serviceDescriptor.ServiceType);
        }

        [Fact]
        public void AddTransientKeyedPlugin_ShouldAddTransientService()
        {
            // Arrange
            var services = new ServiceCollection();
            var pluginType = typeof(MockPlugin);

            // Act
            services.AddTransientKeyedPlugin(pluginType);

            // Assert
            var serviceDescriptor = Assert.Single(services);
            Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
            Assert.Equal(pluginType, serviceDescriptor.ServiceType);
        }

        [Fact]
        public void GetServiceKeyForPluginType_ShouldReturnCustomKey_WhenAttributeIsPresent()
        {
            // Arrange
            var pluginType = typeof(MockPluginWithAttribute);

            // Act
            var serviceKey = pluginType.GetServiceKeyForPluginType();

            // Assert
            Assert.Equal("DP__CustomKey", serviceKey);
        }

        [Fact]
        public void GetServiceKeyForPluginType_ShouldReturnDefaultKey_WhenAttributeIsNotPresent()
        {
            // Arrange
            var pluginType = typeof(MockPlugin);

            // Act
            var serviceKey = pluginType.GetServiceKeyForPluginType();

            // Assert
            Assert.Equal(
                "DP__Microsoft_Greenlight_Extensions_Plugins_Tests_Extensions_PluginRegistrationExtensionsTests+MockPlugin", 
                serviceKey);
        }

        [GreenlightPlugin("CustomName", "CustomKey")]
        private class MockPluginWithAttribute
        {
        }

        private class MockPlugin
        {
        }
    }
}
