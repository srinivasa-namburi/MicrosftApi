using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.DocumentProcess.Shared.Plugins.KmDocs;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.DocumentLibrary;
using Microsoft.Greenlight.Plugins.Default.Utility;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.SemanticKernel;
using Moq;
using System.Reflection;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Extensions
{
    public class PluginExtensionsTests
    {
        [Fact]
        public void AddRegisteredPluginsToKernelPluginCollection_WhenPluginsRegistered_ShouldAddToCollection()
        {
            // Arrange
            var plugins = new KernelPluginCollection();
            var mockPluginImplementation = new Mock<ConversionPlugin>();
            var serviceCollection = new ServiceCollection()
                .AddSingleton(mockPluginImplementation.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pluginAssemblyPath = Directory.GetFiles(baseDirectory, "Microsoft.Greenlight.Plugins.Default.dll")
                .First();

            var assembly = Assembly.LoadFrom(pluginAssemblyPath);

            // Act
            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider);

            // Assert
            Assert.NotEmpty(plugins);
            Assert.Contains(plugins, x => x.Name.Contains("ConversionPlugin"));
        }

        [Fact]
        public void AddRegisteredPluginsToKernelPluginCollection_WhenPluginsTypeExcluded_ShouldNotAddToCollection()
        {
            // Arrange
            var plugins = new KernelPluginCollection();
            var mockPluginImplementation = new Mock<ConversionPlugin>();
            var mockExcludedPluginImplementation = new Mock<DatePlugin>();
            var serviceCollection = new ServiceCollection()
                .AddSingleton(mockPluginImplementation.Object)
                .AddSingleton(mockExcludedPluginImplementation.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pluginAssemblyPath = Directory.GetFiles(baseDirectory, "Microsoft.Greenlight.Plugins.Default.dll")
                .First();

            var assembly = Assembly.LoadFrom(pluginAssemblyPath);

            // Act
            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider, typeof(DatePlugin));

            // Assert
            Assert.NotEmpty(plugins);
            Assert.Contains(plugins, x => x.Name.Contains("ConversionPlugin"));
            Assert.DoesNotContain(plugins, x => x.Name.Contains("DatePlugin"));
        }
    }
}
