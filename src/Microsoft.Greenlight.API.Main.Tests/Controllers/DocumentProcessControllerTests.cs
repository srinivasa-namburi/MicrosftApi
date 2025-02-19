using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using System.Text.Json;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class DocumentProcessControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public DocumentProcessControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class DocumentProcessControllerTests : IDisposable, IClassFixture<DocumentProcessControllerFixture>
    {
        private readonly Mock<IPluginService> _pluginServiceMock = new();
        private readonly Mock<IPublishEndpoint> _publishEndpointMock = new();
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock = new();
        private readonly Mock<IDocumentLibraryInfoService> _documentLibraryInfoServiceMock = new();

        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly IMapper _mapper;
        private readonly DocumentProcessController _controller;

        private const string testContainer = "test-container";
        private const string testAutoImportFolder = "test-auto-import-folder";
        private const string uniqueTestProcess = "unique-test-process";
        private const string existingTestProcess = "existing-test-process";
        private const string updatedTestProcess = "updated-test-process";
        private const string testName = "Test Name";
        private const string testDisplayName = "Test Display Name";
        private const string newName = "New Name";
        private const string newDisplayName = "New Display Name";
        private const string testPlugin = "Test Plugin";
        private const string testBlobContainer = "TestContainer";

        public DocumentProcessControllerTests(DocumentProcessControllerFixture fixture)
        {
            var fakeConfiguration = new Mock<IConfiguration>().Object;
            AdminHelper.Initialize(fakeConfiguration);
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<DocumentProcessInfoProfile>();
                cfg.CreateMap<DocumentProcessMetadataFieldInfo, DynamicDocumentProcessMetaDataField>();
            });
            _mapper = mapperConfig.CreateMapper();
            _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);
        }
        public void Dispose()
        {
            AdminHelper.Initialize(null);
        }

        [Fact]
        public async Task GetAllDocumentProcesses_WhenNoDocumentProcessesExist_ReturnsNotFound()
        {
            // Arrange
            _documentProcessInfoServiceMock.Setup(service => service.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync([]);

            // Act
            var result = await _controller.GetAllDocumentProcesses();

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentProcessById_WhenDocumentProcessNotFound_ReturnsNotFound()
        {
            // Arrange
            var processId = Guid.NewGuid();

            _documentProcessInfoServiceMock.Setup(service => service.GetDocumentProcessInfoByIdAsync(processId))
                .ReturnsAsync((DocumentProcessInfo?)null);
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.GetDocumentProcessById(processId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentProcessesByLibraryId_WhenNoDocumentProcessesExist_ReturnsNotFound()
        {
            // Arrange
            var libraryId = Guid.NewGuid();

            _documentProcessInfoServiceMock.Setup(service => service.GetDocumentProcessesByLibraryIdAsync(libraryId))
                .ReturnsAsync([]);
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.GetDocumentProcessesByLibraryId(libraryId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateDocumentProcess_WhenDocumentProcessNotFound_ReturnsNotFound()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var documentProcess = new DocumentProcessInfo
            {
                Id = processId,
                ShortName = updatedTestProcess
            };
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.UpdateDocumentProcess(processId, documentProcess);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeleteDocumentProcess_WhenDocumentLibrariesExist_DisassociatesDocumentLibraries()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var libraryId = Guid.NewGuid();
            var documentLibraries = new List<DocumentLibraryInfo> { new() { Id = libraryId } };

            _pluginServiceMock.Setup(service => service.GetPluginsByDocumentProcessIdAsync(processId))
                .ReturnsAsync([]);
            _documentLibraryInfoServiceMock.Setup(service => service.GetDocumentLibrariesByProcessIdAsync(processId))
                .ReturnsAsync(documentLibraries);
            _documentLibraryInfoServiceMock.Setup(service => service.DisassociateDocumentProcessAsync(libraryId, processId))
                .Returns(Task.CompletedTask);
            _documentProcessInfoServiceMock.Setup(service => service.DeleteDocumentProcessInfoAsync(processId))
                .ReturnsAsync(true);

            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            await _controller.DeleteDocumentProcess(processId);

            // Assert
            _documentLibraryInfoServiceMock.Verify(service => service.DisassociateDocumentProcessAsync(libraryId, processId), Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentProcess_WhenPluginsExist_CallsDisassociatePluginFromDocumentProcessAsyncOnce()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var pluginId = Guid.NewGuid();
            var plugins = new List<DynamicPlugin> { new() { Id = pluginId, Name = testPlugin, BlobContainerName = testBlobContainer } };

            _pluginServiceMock.Setup(service => service.GetPluginsByDocumentProcessIdAsync(processId))
                .ReturnsAsync(plugins);
            _documentLibraryInfoServiceMock.Setup(service => service.GetDocumentLibrariesByProcessIdAsync(processId))
                .ReturnsAsync([]);
            _documentProcessInfoServiceMock.Setup(service => service.DeleteDocumentProcessInfoAsync(processId))
                .ReturnsAsync(true);

            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            await _controller.DeleteDocumentProcess(processId);

            // Assert
            _pluginServiceMock.Verify(service => service.DisassociatePluginFromDocumentProcessAsync(pluginId, processId), Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentProcess_WhenCalledWithExistingId_CallsDeleteDocumentProcessInfoAsyncOnce()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var existingDocumentProcess = new DynamicDocumentProcessDefinition
            {
                Id = processId,
                ShortName = uniqueTestProcess,
                BlobStorageContainerName = testContainer,
                BlobStorageAutoImportFolderName = testAutoImportFolder
            };

            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Add(existingDocumentProcess);
            _docGenerationDbContext.SaveChanges();

            _pluginServiceMock.Setup(service => service.GetPluginsByDocumentProcessIdAsync(processId))
                .ReturnsAsync([]);
            _documentLibraryInfoServiceMock.Setup(service => service.GetDocumentLibrariesByProcessIdAsync(processId))
                .ReturnsAsync([]);
            _documentProcessInfoServiceMock.Setup(service => service.DeleteDocumentProcessInfoAsync(processId))
                .ReturnsAsync(true);
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            await _controller.DeleteDocumentProcess(processId);

            // Assert
            _documentProcessInfoServiceMock.Verify(service => service.DeleteDocumentProcessInfoAsync(processId), Times.Once);

            // Cleanup
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Remove(existingDocumentProcess);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task ExportDocumentProcess_WhenDocumentProcessExists_ReturnsCorrectExportModel()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var documentProcessModel = new DynamicDocumentProcessDefinition
            {
                Id = processId,
                ShortName = existingTestProcess,
                BlobStorageContainerName = testContainer,
                BlobStorageAutoImportFolderName = testAutoImportFolder,
                Prompts = new List<PromptImplementation>
                {
                    new PromptImplementation
                    {
                        Id = Guid.NewGuid(),
                        DocumentProcessDefinitionId = processId,
                        PromptDefinitionId = Guid.NewGuid(),
                        Text = "Sample Text",
                        PromptDefinition = new PromptDefinition
                        {
                            Id = Guid.NewGuid(),
                            ShortCode = "TestPrompt"
                        }
                    }
                }
            };

            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Add(documentProcessModel);
            _docGenerationDbContext.SaveChanges(); 

            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.ExportDocumentProcess(processId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var exportJson = Assert.IsType<string>(okResult.Value);
            var exportModel = JsonSerializer.Deserialize<DocumentProcessExportInfo>(exportJson);
            Assert.NotNull(exportModel);
            Assert.Equal(documentProcessModel.ShortName, exportModel.DocumentProcessShortName);
            Assert.Equal(documentProcessModel.Id, processId);

            // Cleanup
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Remove(documentProcessModel);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task ExportDocumentProcess_WhenDocumentProcessNotFound_ReturnsNotFound()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.ExportDocumentProcess(processId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentProcessMetadataFields_WhenNoMetadataFieldsExist_ReturnsNotFound()
        {
            // Arrange
            var processId = Guid.NewGuid();

            _docGenerationDbContext.DynamicDocumentProcessMetaDataFields.RemoveRange
                (_docGenerationDbContext.DynamicDocumentProcessMetaDataFields);
            _docGenerationDbContext.SaveChanges();

            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.GetDocumentProcessMetadataFields(processId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateOrUpdateDocumentProcessMetadataFields_WhenNoMetadataFields_ReturnsBadRequest()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var metadataFields = new List<DocumentProcessMetadataFieldInfo>();
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.CreateOrUpdateDocumentProcessMetadataFields(processId, metadataFields);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task CreateOrUpdateDocumentProcessMetadataFields_WhenIdIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var processId = Guid.Empty;
            var metadataFields = new List<DocumentProcessMetadataFieldInfo>
            {
                new() {
                    Id = Guid.NewGuid(),
                    DynamicDocumentProcessDefinitionId = processId,
                    FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
                    Name = testName,
                    DisplayName = testDisplayName,
                    IsRequired = true,
                    Order = 1
                }
            };
            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.CreateOrUpdateDocumentProcessMetadataFields(processId, metadataFields);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
 
        public async Task CreateOrUpdateDocumentProcessMetadataFields_WhenMetadataFieldsExist_UpdatesToDatabaseCorrectly()
        {
            // Arrange
            var processId = Guid.NewGuid();
            var documentProcess = new DynamicDocumentProcessDefinition
            {
                Id = processId,
                ShortName = existingTestProcess,
                BlobStorageContainerName = testContainer,
                BlobStorageAutoImportFolderName = testAutoImportFolder
            };
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Add(documentProcess);
            _docGenerationDbContext.SaveChanges();

            var existingMetadataField = new DynamicDocumentProcessMetaDataField
            {
                Id = Guid.NewGuid(),
                DynamicDocumentProcessDefinitionId = processId,
                FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
                Name = testName,
                DisplayName = testDisplayName,
                IsRequired = true,
                Order = 1
            };
            _docGenerationDbContext.DynamicDocumentProcessMetaDataFields.Add(existingMetadataField);
            _docGenerationDbContext.SaveChanges();

            var updatedMetadataFields = new List<DocumentProcessMetadataFieldInfo>
            {
                new()
                {
                    Id = existingMetadataField.Id,
                    DynamicDocumentProcessDefinitionId = processId,
                    FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
                    Name = newName,
                    DisplayName = newDisplayName,
                    IsRequired = true,
                    Order = 1
                }
            };

            var _controller = new DocumentProcessController(
                _docGenerationDbContext,
                _documentProcessInfoServiceMock.Object,
                _pluginServiceMock.Object,
                _documentLibraryInfoServiceMock.Object,
                _mapper,
                _publishEndpointMock.Object);

            // Act
            var result = await _controller.CreateOrUpdateDocumentProcessMetadataFields(processId, updatedMetadataFields);

            // Assert
            Assert.Contains(_docGenerationDbContext.DynamicDocumentProcessMetaDataFields, fields => fields.Name == newName);
            Assert.Contains(_docGenerationDbContext.DynamicDocumentProcessMetaDataFields, fields => fields.DisplayName == newDisplayName);
            Assert.Contains(_docGenerationDbContext.DynamicDocumentProcessMetaDataFields, fields => fields.Id == existingMetadataField.Id);

            // Cleanup
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Remove(documentProcess);
            _docGenerationDbContext.DynamicDocumentProcessMetaDataFields.Remove(existingMetadataField);
            _docGenerationDbContext.SaveChanges();
        }

    }
}
