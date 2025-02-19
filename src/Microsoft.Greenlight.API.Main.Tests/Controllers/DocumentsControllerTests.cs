using AutoMapper;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class DocumentControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public DocumentControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class DocumentsControllerTests : IClassFixture<DocumentControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly AzureFileHelper _azureFileHelper;
        private readonly IMapper _mapper;
        private readonly Mock<IPublishEndpoint> _publishEndpoint;
        private readonly Mock<BlobServiceClient> _blobServiceClientMock;
        private readonly Mock<IDocumentExporter> _wordDocumentExporterMock;

        public DocumentsControllerTests(DocumentControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _blobServiceClientMock = new Mock<BlobServiceClient>();
            _azureFileHelper = new AzureFileHelper(_blobServiceClientMock.Object, _docGenerationDbContext);
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<GeneratedDocumentProfile>())
                .CreateMapper();
            _publishEndpoint = new Mock<IPublishEndpoint>();
            _wordDocumentExporterMock = new Mock<IDocumentExporter>();
        }

        [Fact]
        public async Task GetFullGeneratedDocument_WithValidDTO_ShouldReturnOk()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var document = new GeneratedDocument { Id = Guid.Parse(documentId), Title = "Test Document" };
            _docGenerationDbContext.GeneratedDocuments.Add(document);
            _docGenerationDbContext.SaveChanges();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetFullGeneratedDocument(documentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var documentInfo = Assert.IsType<GeneratedDocumentInfo>(okResult.Value);
            Assert.Equal(documentId, documentInfo.Id.ToString());

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(document);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task GetPreparedDocumentLink_WhenNoDocumentLinksFound_ShouldReturnNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetPreparedDocumentLink(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentExportFile_WithInvalidExporterType_ShouldReturnBadRequest()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var document = new GeneratedDocument 
            { 
                Id = Guid.Parse(documentId), 
                Title = "Test Document" 
            };
            _docGenerationDbContext.GeneratedDocuments.Add(document);
            _docGenerationDbContext.SaveChanges();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetDocumentExportFile(documentId, "InvalidExporter");

            // Assert
            Assert.IsType<BadRequestResult>(result);

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(document);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task GetDocumentExportFile_WithInvalidDocumentId_ShouldReturnNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetDocumentExportFile(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetDocumentExportFile_WithTitleNumbering_ShouldReturnFile()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var document = new GeneratedDocument
            {
                Id = Guid.Parse(documentId),
                Title = "Test Document"
            };
            _docGenerationDbContext.GeneratedDocuments.Add(document);
            var contentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                GeneratedDocumentId = document.Id,
                Text = "1. Introduction",
                Type = ContentNodeType.Title
            }; 
            _docGenerationDbContext.ContentNodes.Add(contentNode);
            _docGenerationDbContext.SaveChanges();

            _wordDocumentExporterMock
                .Setup(e => e.ExportDocumentAsync(It.IsAny<GeneratedDocument>(), It.IsAny<bool>()))
                .ReturnsAsync(new MemoryStream());

            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetDocumentExportFile(documentId);

            // Assert
            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/octet-stream", fileResult.ContentType);
            Assert.Equal($"{document.Title}.docx", fileResult.FileDownloadName);

            // Cleanup
            _docGenerationDbContext.ContentNodes.Remove(contentNode);
            _docGenerationDbContext.GeneratedDocuments.Remove(document);
            _docGenerationDbContext.SaveChanges();
            
        }

        [Fact]
        public async Task GetDocumentExportPermalink_WhenDocumentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.GetDocumentExportPermalink(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentExportPermalink_DocumentNotNullAndNotExported_CallsUploadFileToBlobAsync()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var document = new GeneratedDocument
            {
                Id = documentId,
                Title = "Test Document"
            };

            _docGenerationDbContext.GeneratedDocuments.Add(document);
            _docGenerationDbContext.SaveChanges();

            _wordDocumentExporterMock.Setup(exporter => exporter.ExportDocumentAsync
            (
                It.IsAny<GeneratedDocument>(), 
                It.IsAny<bool>())).ReturnsAsync(new MemoryStream());

            var fileHelperMock = new Mock<AzureFileHelper>(_blobServiceClientMock.Object, _docGenerationDbContext);
            fileHelperMock.Setup(helper => helper.UploadFileToBlobAsync
            (
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>())
            ).ReturnsAsync("https://example.com/blob");
            fileHelperMock.Setup(helper => helper.SaveFileInfoAsync
            (
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<Guid>())
            ).ReturnsAsync
            (
                new ExportedDocumentLink
                {
                    Id = Guid.NewGuid(),
                    MimeType = "application/octet-stream",
                    AbsoluteUrl = "https://example.com/blob",
                    BlobContainer = "test-container",
                    FileName = "test-file.docx"
                }
            );

            var controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                fileHelperMock.Object,
                _mapper
            );

            // Act
            await controller.GetDocumentExportPermalink(documentId.ToString());

            // Assert
            fileHelperMock.Verify(helper => helper.UploadFileToBlobAsync
            (
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>()), 
                Times.Once
            );

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(document);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task DeleteDocument_WithInvalidDocumentId_ShouldReturnNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid().ToString();
            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.DeleteDocument(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
        [Fact]
        public async Task DeleteDocument_WithDocumentHavingContentNodes_ShouldDeleteAllNodes()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var document = new GeneratedDocument
            {
                Id = documentId,
                Title = "Test Document"
            };
            var contentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                GeneratedDocumentId = documentId,
                Text = "Test Content Node"
            };
            _docGenerationDbContext.GeneratedDocuments.Add(document);
            _docGenerationDbContext.ContentNodes.Add(contentNode);
            _docGenerationDbContext.SaveChanges();

            var _controller = new DocumentsController(
                _publishEndpoint.Object,
                _docGenerationDbContext,
                _wordDocumentExporterMock.Object,
                _azureFileHelper,
                _mapper
            );

            // Act
            var result = await _controller.DeleteDocument(documentId.ToString());

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _docGenerationDbContext.GeneratedDocuments.FindAsync(documentId));
            Assert.Null(await _docGenerationDbContext.ContentNodes.FindAsync(contentNode.Id));
        }
    }
}
