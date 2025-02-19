using AutoMapper;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class PluginsControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public PluginsControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class PluginsControllerTests : IDisposable, IClassFixture<PluginsControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly IMapper _mapper;
        private readonly Mock<AzureFileHelper> _azureFileHelperMock;
        private readonly Mock<IPluginService> _pluginService;
        private readonly Mock<IPublishEndpoint> _publishEndpoint;
        private readonly Mock<BlobServiceClient> _blobServiceClientMock;

        public PluginsControllerTests(PluginsControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<PluginMappingProfile>()).CreateMapper();
            _blobServiceClientMock = new Mock<BlobServiceClient>();
            _azureFileHelperMock = new Mock<AzureFileHelper>(_blobServiceClientMock.Object, _docGenerationDbContext);
            _pluginService = new Mock<IPluginService>();
            _publishEndpoint = new Mock<IPublishEndpoint>();

            var fakeConfiguration = new Mock<IConfiguration>().Object;
            AdminHelper.Initialize(fakeConfiguration);
        }
        public void Dispose()
        {
            AdminHelper.Initialize(null);
        }

        [Fact]
        public async Task GetPluginById_WhenPluginDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var pluginId = Guid.NewGuid();
            _pluginService.Setup(service => service.GetPluginByIdAsync(pluginId)).ReturnsAsync((DynamicPlugin?)null);
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.GetPluginById(pluginId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task AssociateWithDocumentProcess_WhenSuccessful_CallsAssociatePluginWithDocumentProcessAsyncOnce()
        {
            // Arrange
            var pluginId = Guid.NewGuid();
            var documentProcessId = Guid.NewGuid();
            var version = "1.0.0";
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.AssociateWithDocumentProcess(pluginId, documentProcessId, version);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _pluginService.Verify(service => service.AssociatePluginWithDocumentProcessAsync(pluginId, documentProcessId, version),
                Times.Once);
        }

        [Fact]
        public async Task AssociateWithDocumentProcess_WhenInvalidOperationExceptionIsThrown_ReturnsBadRequest()
        {
            // Arrange
            var pluginId = Guid.NewGuid();
            var documentProcessId = Guid.NewGuid();
            var version = "1.0.0";
            _pluginService.Setup(service => service.AssociatePluginWithDocumentProcessAsync(pluginId, documentProcessId, version))
                .ThrowsAsync(new InvalidOperationException("Invalid operation"));
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.AssociateWithDocumentProcess(pluginId, documentProcessId, version);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid operation", badRequestResult.Value);
        }

        [Fact]
        public async Task DisassociateFromDocumentProcess_WhenSuccessful_CallsDissassociatePluginWithDocumentProcessAsyncOnce()
        {
            // Arrange
            var pluginId = Guid.NewGuid();
            var documentProcessId = Guid.NewGuid();
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.DisassociateFromDocumentProcess(pluginId, documentProcessId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _pluginService.Verify(service => service.DisassociatePluginFromDocumentProcessAsync
            (pluginId, documentProcessId), Times.Once);
        }

        [Fact]
        public async Task UploadPlugin_WithNewPluginInfo_UpdatesDatabaseCorrectly()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a Fake File";
            var fileName = "NewPlugin_1.0.0.zip";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            _azureFileHelperMock.Setup(helper => helper.UploadFileToBlobAsync
            (
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), true)
            ).ReturnsAsync("http://fakeurl.com");
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(fileMock.Object);

            // Assert
            var plugin = await _docGenerationDbContext.DynamicPlugins.FirstOrDefaultAsync(p => p.Name == "NewPlugin");
            Assert.NotNull(plugin);
            Assert.Equal("plugins", plugin.BlobContainerName);
            Assert.Single(plugin.Versions);
            Assert.Contains(plugin.Versions, v => v.Equals(new DynamicPluginVersion(1, 0, 0)));
        }

        [Fact]
        public async Task UploadPlugin_WhenVersionExists_OverwritesExistingVersion()
        {
            // Arrange
            var existingPlugin = new DynamicPlugin
            {
                Name = "ExistingPlugin",
                BlobContainerName = "plugins",
                Versions =
                [
                    new DynamicPluginVersion(1, 0, 0)
                ]
            };
            _docGenerationDbContext.DynamicPlugins.Add(existingPlugin);
            await _docGenerationDbContext.SaveChangesAsync();

            // IFormFile mock setup
            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a Fake File";
            var fileName = "ExistingPlugin_2.0.0.zip";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            _azureFileHelperMock.Setup(helper => helper.UploadFileToBlobAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(), true)
            ).ReturnsAsync("http://fakeurl.com");

            var _controller = new PluginsController(
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(fileMock.Object);

            // Assert
            var plugin = await _docGenerationDbContext.DynamicPlugins.FirstOrDefaultAsync(p => p.Name == "ExistingPlugin");
            Assert.NotNull(plugin);
            Assert.Contains(plugin.Versions, v => v.Equals(new DynamicPluginVersion(2, 0, 0)));
            _azureFileHelperMock.Verify(helper => helper.UploadFileToBlobAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(), true), Times.Once);

            // Clean up
            _docGenerationDbContext.DynamicPlugins.Remove(existingPlugin);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task UploadPlugin_WhenNoFileIsProvided_ReturnsBadRequest()
        {
            // Arrange
            IFormFile? file = null;
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(file!);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UploadPlugin_WhenInvalidFileTypeIsProvided_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.FileName).Returns("TestPlugin_1.0.0.txt");
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UploadPlugin_WhenInvalidFileNameFormatIsProvided_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.FileName).Returns("TestPlugin.zip");
            var _controller = new PluginsController
            (
                _pluginService.Object,
                _docGenerationDbContext,
                _mapper,
                _azureFileHelperMock.Object,
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UploadPlugin_WhenInvalidVersionFormatIsProvided_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.FileName).Returns("TestPlugin_invalidversion.zip");
            var _controller = new PluginsController
            (
                _pluginService.Object, 
                _docGenerationDbContext, 
                _mapper, 
                _azureFileHelperMock.Object, 
                _publishEndpoint.Object
            );

            // Act
            var result = await _controller.UploadPlugin(fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}