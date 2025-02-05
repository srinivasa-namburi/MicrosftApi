using Moq;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using MassTransit;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.API.Main.Controllers.Tests
{
    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class DocumentLibraryControllerTests : IDisposable
    {
        public void Dispose()
        {
            AdminHelper.Initialize(null);
        }

        [Fact]
        public async Task GetAllDocumentLibraries_WithList_ReturnsOk()
        {
            // Arrange
            var mockService = new Mock<IDocumentLibraryInfoService>();
            mockService.Setup(service => service.GetAllDocumentLibrariesAsync())
                       .ReturnsAsync([
                            new() { Id = Guid.NewGuid(), ShortName = "Library1" },
                            new() { Id = Guid.NewGuid(), ShortName = "Library2" }
                        ]);
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);

            // Act
            var result = await controller.GetAllDocumentLibraries();

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<DocumentLibraryInfo>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<List<DocumentLibraryInfo>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetDocumentLibraryById_WithValidId_ReturnsOk()
        {
            // Arrange & Act
            var testId = Guid.NewGuid();
            var resultDoc = new DocumentLibraryInfo { Id = testId };
            var result = await GetDocumentLibraryByIdArrangeAndActAsync(testId, resultDoc);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<DocumentLibraryInfo>(okResult.Value);
            Assert.Equal(testId, returnValue.Id);
        }

        [Fact]
        public async Task GetDocumentLibraryById_WithInvalidId_ReturnsNotFound()
        {
            // Arrange & Act
            var testId = Guid.NewGuid();
            DocumentLibraryInfo? resultDoc = null;
            var result = await GetDocumentLibraryByIdArrangeAndActAsync(testId, resultDoc);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static Task<ActionResult<DocumentLibraryInfo>> GetDocumentLibraryByIdArrangeAndActAsync(
            Guid id,
            DocumentLibraryInfo? resultDoc)
        {
            // Arrange
            var mockService = new Mock<IDocumentLibraryInfoService>();

            mockService.Setup(service => service.GetDocumentLibraryByIdAsync(id))
                       .ReturnsAsync(resultDoc);
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);

            // Act
            return controller.GetDocumentLibraryById(id);
        }

        [Fact]
        public async Task GetDocumentLibraryByShortName_WithValidShortName_ReturnsOk()
        {
            // Arrange & Act
            var shortName = "Library 1";
            var resultDoc = new DocumentLibraryInfo { ShortName = shortName };
            var result = await GetDocumentLibraryByShortNameArrangeAndActAsync(shortName, resultDoc);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<DocumentLibraryInfo>(okResult.Value);
            Assert.Equal(shortName, returnValue.ShortName);
        }

        [Fact]
        public async Task GetDocumentLibraryByShortName_WithInValidShortName_ReturnsNotFound()
        {
            // Arrange & Act
            var shortName = "Library 1";
            DocumentLibraryInfo? resultDoc = null;
            var result = await GetDocumentLibraryByShortNameArrangeAndActAsync(shortName, resultDoc);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static Task<ActionResult<DocumentLibraryInfo>> GetDocumentLibraryByShortNameArrangeAndActAsync(
            string shortName,
            DocumentLibraryInfo? resultDoc)
        {
            // Arrange
            var mockService = new Mock<IDocumentLibraryInfoService>();

            mockService.Setup(service => service.GetDocumentLibraryByShortNameAsync(shortName))
                       .ReturnsAsync(resultDoc);
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);

            // Act
            return controller.GetDocumentLibraryByShortName(shortName);
        }

        [Fact]
        public async Task GetDocumentLibrariesByProcessId_WithValidProcessId_ReturnsOk()
        {
            // Arrange & Act
            var processId = Guid.NewGuid();
            List<DocumentLibraryInfo> resultDocs = [
                            new() { Id = Guid.NewGuid() },
                            new() { Id = Guid.NewGuid() }
                        ];
            var result = await GetDocumentLibraryByProcessIdArrangeAndActAsync(processId, resultDocs);

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<DocumentLibraryInfo>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<List<DocumentLibraryInfo>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetDocumentLibrariesByProcessId_WithNullLibraryList_ReturnsNotFound()
        {
            // This shouldn't be possible, as the return type on the setup call is not nullable, but just in case
            // Arrange & Act
            var processId = Guid.NewGuid();
            List<DocumentLibraryInfo> resultDocs = null!;
            var result = await GetDocumentLibraryByProcessIdArrangeAndActAsync(processId, resultDocs!);

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<DocumentLibraryInfo>>>(result);
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task GetDocumentLibrariesByProcessId_WithEmptyLibraryList_ReturnsNotFound()
        {
            // Arrange & Act
            var processId = Guid.NewGuid();
            List<DocumentLibraryInfo> resultDocs = [];
            var result = await GetDocumentLibraryByProcessIdArrangeAndActAsync(processId, resultDocs);

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<DocumentLibraryInfo>>>(result);
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static Task<ActionResult<List<DocumentLibraryInfo>>> GetDocumentLibraryByProcessIdArrangeAndActAsync(
            Guid processId,
            List<DocumentLibraryInfo> resultDocs)
        {
            // Arrange
            var mockService = new Mock<IDocumentLibraryInfoService>();

            mockService.Setup(service => service.GetDocumentLibrariesByProcessIdAsync(processId))
                       .ReturnsAsync(resultDocs);
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);

            // Act
            return controller.GetDocumentLibrariesByProcessId(processId);
        }

        [Fact]
        public async Task CreateDocumentLibrary_WithLibraryInfo_ReturnsCreated()
        {
            // Arrange & Act
            var libraryInfo = new DocumentLibraryInfo { Id = Guid.NewGuid() };
            var result = await CreateDocumentLibraryArrangeAndActAsync(libraryInfo);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var returnValue = Assert.IsType<DocumentLibraryInfo>(createdResult.Value);
            Assert.Equal(libraryInfo.Id, returnValue.Id);
        }

        [Fact]
        public async Task CreateDocumentLibrary_WithoutLibraryInfo_ReturnsBadRequest()
        {
            // Arrange & Act
            var result = await CreateDocumentLibraryArrangeAndActAsync(null);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task CreateDocumentLibrary_RunningInProduction_PublishesRestartRequest()
        {
            // Arrange & Act
            var libraryInfo = new DocumentLibraryInfo { Id = Guid.NewGuid() };
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var result = await CreateDocumentLibraryArrangeAndActAsync(libraryInfo, mockPublishEndpoint, true);

            // Assert
            mockPublishEndpoint.Verify(publish => publish.Publish(It.IsAny<RestartWorker>(), default), Times.Once);
        }

        private static Task<ActionResult<DocumentLibraryInfo>> CreateDocumentLibraryArrangeAndActAsync(
            DocumentLibraryInfo? libraryInfo,
            Mock<IPublishEndpoint>? mockPublishEndpoint = null,
            bool isProduction = false)
        {
            // Arrange
            var configuration = new Mock<IConfiguration>();
            if (isProduction) {
                configuration.Setup(config => config["CONTAINER_APP_ENV"])
                         .Returns("isProduction");
            }
            AdminHelper.Initialize(configuration.Object);

            var mockService = new Mock<IDocumentLibraryInfoService>();
            mockService.Setup(service => service.CreateDocumentLibraryAsync(libraryInfo))
                       .ReturnsAsync(libraryInfo!);

            mockPublishEndpoint ??= new Mock<IPublishEndpoint>();

            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);
            // Act
            return controller.CreateDocumentLibrary(libraryInfo);
        }

        [Fact]
        public async Task UpdateDocumentLibrary_WithValidContent_ReturnsCreated()
        {
            // Arrange & Act
            var libraryInfo = new DocumentLibraryInfo { Id = Guid.NewGuid() };
            var result = await UpdateDocumentLibraryArrangeAndActAsync(libraryInfo.Id, libraryInfo);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<DocumentLibraryInfo>(okResult.Value);
            Assert.Equal(libraryInfo.Id, returnValue.Id);
        }

        [Fact]
        public async Task UpdateDocumentLibrary_WithNullDocumentInfo_ReturnsBadRequest()
        {
            // Arrange & Act
            var libraryInfo = new DocumentLibraryInfo { Id = Guid.NewGuid() };
            var result = await UpdateDocumentLibraryArrangeAndActAsync(libraryInfo.Id, null);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task UpdateDocumentLibrary_WithMiMatchedIds_ReturnsBadRequest()
        {
            // Arrange & Act
            var libraryInfo = new DocumentLibraryInfo { Id = Guid.NewGuid() };
            var result = await UpdateDocumentLibraryArrangeAndActAsync(Guid.NewGuid(), libraryInfo);

            // Assert
            var actionResult = Assert.IsType<ActionResult<DocumentLibraryInfo>>(result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        private static Task<ActionResult<DocumentLibraryInfo>> UpdateDocumentLibraryArrangeAndActAsync(
            Guid id,
            DocumentLibraryInfo? libraryInfo)
        {
            // Arrange
            var mockService = new Mock<IDocumentLibraryInfoService>();
            mockService.Setup(service => service.UpdateDocumentLibraryAsync(libraryInfo))
                       .ReturnsAsync(libraryInfo!);
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);

            // Act
            return controller.UpdateDocumentLibrary(id, libraryInfo);
        }

        [Fact]
        public async Task DeleteDocumentLibrary_WithValidId_ReturnsNoContent()
        {
            // Arrange & Act
            var testId = Guid.NewGuid();
            var result = await DeleteDocumentLibraryArrangeAndActAsync(testId, true);

            // Assert
            var actionResult = Assert.IsAssignableFrom<ActionResult>(result);
            var noContentResult = Assert.IsType<NoContentResult>(actionResult);
        }

        [Fact]
        public async Task DeleteDocumentLibrary_WithInValidId_ReturnsNotFound()
        {
            // Arrange & Act
            var testId = Guid.NewGuid();
            var result = await DeleteDocumentLibraryArrangeAndActAsync(testId, false);

            // Assert
            var actionResult = Assert.IsAssignableFrom<ActionResult>(result);
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
        }

        [Fact]
        public async Task DeleteDocumentLibrary_RunningInProduction_PublishesRestartRequest()
        {
            // Arrange & Act
            var testId = Guid.NewGuid();
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var result = await DeleteDocumentLibraryArrangeAndActAsync(testId, true, mockPublishEndpoint, true);

            // Assert
            mockPublishEndpoint.Verify(publish => publish.Publish(It.IsAny<RestartWorker>(), default), Times.Once);
        }

        private static Task<IActionResult> DeleteDocumentLibraryArrangeAndActAsync(
            Guid id,
            bool isSuccessful,
            Mock<IPublishEndpoint>? mockPublishEndpoint = null,
            bool isProduction = false)
        {
            // Arrange
            var configuration = new Mock<IConfiguration>();
            if (isProduction)
            {
                configuration.Setup(config => config["CONTAINER_APP_ENV"])
                         .Returns("isProduction");
            }
            AdminHelper.Initialize(configuration.Object);
            var mockService = new Mock<IDocumentLibraryInfoService>();
            mockService.Setup(service => service.DeleteDocumentLibraryAsync(id))
                       .ReturnsAsync(isSuccessful);
            mockPublishEndpoint ??= new Mock<IPublishEndpoint>();
            var controller = new DocumentLibraryController(mockService.Object, mockPublishEndpoint.Object);
            // Act
            return controller.DeleteDocumentLibrary(id);
        }
    }
}