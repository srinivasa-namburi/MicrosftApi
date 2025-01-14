using Microsoft.Greenlight.Shared.Services;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.API.Main.Controllers.Tests
{
    public class PromptsControllerTests
    {
        [Fact]
        public async Task GetPromptsByProcessId_WithValidProcessId_ReturnsOk()
        {
            // Arrange & Act
            var processId = Guid.NewGuid();
            List<PromptInfo> promptInfoList = [
                    new PromptInfo{ShortCode = "Code 1", Text = "Text 1"},
                    new PromptInfo{ShortCode = "Code 2", Text = "Text 2"}
                ];
            var result = await GetPromptsByProcessIdArrangeAndActAsync(processId, promptInfoList);

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<PromptInfo>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<List<PromptInfo>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPromptsByProcessId_WithInValidProcessId_ReturnsNotFound()
        {
            // Arrange & Act
            var processId = Guid.NewGuid();
            List<PromptInfo> promptInfoList = [];
            var result = await GetPromptsByProcessIdArrangeAndActAsync(processId, promptInfoList);

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<PromptInfo>>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static Task<ActionResult<List<PromptInfo>>> GetPromptsByProcessIdArrangeAndActAsync(
            Guid processId,
            List<PromptInfo> promptInfoList)
        {
            // Arrange
            var mockService = new Mock<IPromptInfoService>();
            mockService.Setup(x => x.GetPromptsByProcessIdAsync(processId))
                .ReturnsAsync(promptInfoList);
            var controller = new PromptsController(mockService.Object);

            // Act
            return controller.GetPromptsByProcessId(processId);
        }

        [Fact]
        public async Task GetPromptById_WithValidId_ReturnsOk()
        {
            // Arrange & Act
            var id = Guid.NewGuid();
            var promptInfo = new PromptInfo { ShortCode = "Code 1", Text = "Text 1" };
            var result = await GetPromptByIdArrangeAndActAsync(id, promptInfo);

            // Assert
            var actionResult = Assert.IsType<ActionResult<PromptInfo>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<PromptInfo>(okResult.Value);
            Assert.Equal("Code 1", returnValue.ShortCode);
            Assert.Equal("Text 1", returnValue.Text);
        }

        [Fact]
        public async Task GetPromptById_WithInValidId_ReturnsNotFound()
        {
            // Arrange & Act
            var id = Guid.NewGuid();
            PromptInfo promptInfo = null!;
            var result = await GetPromptByIdArrangeAndActAsync(id, promptInfo);

            // Assert
            var actionResult = Assert.IsType<ActionResult<PromptInfo>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static Task<ActionResult<PromptInfo>> GetPromptByIdArrangeAndActAsync(
            Guid id,
            PromptInfo? promptInfo)
        {
            // Arrange
            var mockService = new Mock<IPromptInfoService>();
            mockService.Setup(x => x.GetPromptByIdAsync(id))
                .ReturnsAsync(promptInfo);
            var controller = new PromptsController(mockService.Object);
            // Act
            return controller.GetPromptById(id);
        }

        [Fact]
        public async Task UpdatePrompt_WithMatchingIds_ReturnsAccepted()
        {
            // Arrange & Act
            var id = Guid.NewGuid();
            var promptInfo = new PromptInfo { Id = id, ShortCode = "Code 1", Text = "Text 1" };
            var result = await UpdatePromptArrangeAndActAsync(id, promptInfo);

            // Assert
            var actionResult = Assert.IsAssignableFrom<ActionResult>(result);
            Assert.IsType<AcceptedResult>(actionResult);
        }

        [Fact]
        public async Task UpdatePrompt_WithMissMatchingIds_ReturnsBadRequet()
        {
            // Arrange & Act
            var id = Guid.NewGuid();
            var promptInfo = new PromptInfo { Id = Guid.NewGuid(), ShortCode = "Code 1", Text = "Text 1" };
            var result = await UpdatePromptArrangeAndActAsync(id, promptInfo);

            // Assert
            var actionResult = Assert.IsAssignableFrom<ActionResult>(result);
            Assert.IsType<BadRequestResult>(actionResult);
        }

        private static Task<ActionResult> UpdatePromptArrangeAndActAsync(
            Guid id,
            PromptInfo promptInfo)
        {
            // Arrange
            var mockService = new Mock<IPromptInfoService>();
          
            var controller = new PromptsController(mockService.Object);
            // Act
            return controller.UpdatePrompt(id, promptInfo);
        }
    }
}