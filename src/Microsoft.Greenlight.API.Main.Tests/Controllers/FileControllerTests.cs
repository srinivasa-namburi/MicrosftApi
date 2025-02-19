using AutoMapper;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class FileControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public FileControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class FileControllerTests : IClassFixture<FileControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly AzureFileHelper _azureFileHelper;
        private readonly IMapper _mapper;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly Mock<BlobServiceClient> _blobServiceClientMock;

        private const string testContainerName = "test-container";
        private const string invalidContainerName = "invalid-container";
        private const string testFileName = "test-file.txt";
        private const string invalidFileName = " ";
        private const string testFileUrl = "https://fakeurl.com/blob/test-file.txt";
        private const string mimeType = "application/octet-stream";

        public FileControllerTests(FileControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _blobServiceClientMock = new Mock<BlobServiceClient>();
            _azureFileHelper = new AzureFileHelper(_blobServiceClientMock.Object, _docGenerationDbContext);
            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<ExportedDocumentLinkProfile>()).CreateMapper();
        }

        [Fact]
        public async Task DownloadFile_WhenFileStreamIsNull_ReturnsNotFound()
        {
            // Arrange
            var decodedFileUrl = Uri.UnescapeDataString(testFileUrl);

            var azureFileHelperMock = new Mock<AzureFileHelper>
            (
                _blobServiceClientMock.Object,
                _docGenerationDbContext
            );
            azureFileHelperMock.Setup(x => x.GetFileAsStreamFromFullBlobUrlAsync(decodedFileUrl))
                .ReturnsAsync((Stream?)null);

            var _controller = new FileController
            (
                azureFileHelperMock.Object,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.DownloadFile(testFileUrl);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DownloadFileById_WhenFileIsNull_ReturnsNotFound()
        {
            // Arrange
            var linkId = Guid.NewGuid().ToString();
            var azureFileHelperMock = new Mock<AzureFileHelper>
            (
                _blobServiceClientMock.Object,
                _docGenerationDbContext
            );

            var _controller = new FileController
            (
                azureFileHelperMock.Object,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.DownloadFileById(linkId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DownloadFileById_WhenStreamIsNull_ReturnsNotFound()
        {
            // Arrange
            var linkId = Guid.NewGuid().ToString();
            var exportedDocumentLink = new ExportedDocumentLink
            {
                AbsoluteUrl = testFileUrl,
                BlobContainer = testContainerName,
                FileName = testFileName,
                MimeType = mimeType
            };

            _docGenerationDbContext.ExportedDocumentLinks.Add(exportedDocumentLink);
            _docGenerationDbContext.SaveChanges();

            var azureFileHelperMock = new Mock<AzureFileHelper>
            (
                _blobServiceClientMock.Object,
                _docGenerationDbContext
            );
            azureFileHelperMock.Setup(x => x.GetFileAsStreamFromFullBlobUrlAsync(exportedDocumentLink
                .AbsoluteUrl))
                .ReturnsAsync((Stream?)null);

            var _controller = new FileController
            (
                azureFileHelperMock.Object,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.DownloadFileById(linkId);

            // Assert
            Assert.IsType<NotFoundResult>(result);

            // Clean up
            _docGenerationDbContext.ExportedDocumentLinks.Remove(exportedDocumentLink);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task UploadFile_WhenFileProvidedIsNull_ReturnsBadRequest()
        {
            // Arrange
            IFormFile? file = null;
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFile(testContainerName, testFileName, file);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFile_WhenFileLengthIsZero_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(0);
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFile(testContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFile_WhenContainerNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFile(invalidContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFile_WhenFileNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length
            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFile(testContainerName, invalidFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileDirect_WhenFileProvidedIsNull_ReturnsBadRequest()
        {
            // Arrange
            IFormFile? file = null;
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileDirect(testContainerName, testFileName, file);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileDirect_WhenFileLengthIsZero_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(0);
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileDirect(testContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileDirect_WhenContainerNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileDirect(invalidContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileDirect_WhenFileNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length
            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileDirect(testContainerName, invalidFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileReturnFileInfo_WhenFileProvidedIsNull_ReturnsBadRequest()
        {
            // Arrange
            IFormFile? file = null;
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileReturnFileInfo(testContainerName, testFileName, file!);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileReturnFileInfo_WhenFileLengthIsZero_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(0);
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );
            // Act
            var result = await _controller.UploadFileReturnFileInfo(testContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileReturnFileInfo_WhenContainerNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length
            var azureFileHelperMock = new Mock<AzureFileHelper>
            (
                _blobServiceClientMock.Object,
                _docGenerationDbContext
            );
            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync([]);
            var _controller = new FileController
            (
                azureFileHelperMock.Object,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileReturnFileInfo(invalidContainerName, testFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadFileReturnFileInfo_WhenFileNameIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(1024); // Mock a non-zero file length
            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync([]);
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.UploadFileReturnFileInfo(testContainerName, invalidFileName, fileMock.Object);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetFileInfo_WhenFileInfoModelIsNull_ReturnsNotFound()
        {
            // Arrange
            var assetId = Guid.NewGuid().ToString(); // Generate a valid GUID
            var fileAccessUrl = $"https://fakeurl.com/blob/{assetId}"; // Ensure the URL contains a valid GUID
            var azureFileHelperMock = new Mock<AzureFileHelper>
            (
                _blobServiceClientMock.Object,
                _docGenerationDbContext
            );

            var _controller = new FileController
            (
                azureFileHelperMock.Object,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.GetFileInfo(fileAccessUrl);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetFileInfo_WhenFileInfoExists_ReturnsExpectedFileInfo()
        {
            // Arrange
            var assetId = Guid.NewGuid();
            var fileAccessUrl = $"https://fakeurl.com/blob/{assetId}";
            var exportedDocumentLink = new ExportedDocumentLink
            {
                Id = assetId,
                AbsoluteUrl = fileAccessUrl,
                BlobContainer = testContainerName,
                FileName = testFileName,
                MimeType = mimeType
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(exportedDocumentLink);
            await _docGenerationDbContext.SaveChangesAsync();
            var _controller = new FileController
            (
                _azureFileHelper,
                _docGenerationDbContext,
                _mapper,
                _documentProcessInfoServiceMock.Object
            );

            // Act
            var result = await _controller.GetFileInfo(fileAccessUrl);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var fileInfo = Assert.IsType<ExportedDocumentLinkInfo>(okResult.Value);
            Assert.Equal(exportedDocumentLink.FileName, fileInfo.FileName);
            Assert.Equal(exportedDocumentLink.AbsoluteUrl, fileInfo.AbsoluteUrl);
            Assert.Equal(exportedDocumentLink.BlobContainer, fileInfo.BlobContainer);
            Assert.Equal(exportedDocumentLink.MimeType, fileInfo.MimeType);

            // Cleanup
            _docGenerationDbContext.ExportedDocumentLinks.RemoveRange(_docGenerationDbContext.ExportedDocumentLinks);
            await _docGenerationDbContext.SaveChangesAsync();
        }
    }
}
