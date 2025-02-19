using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Microsoft.Greenlight.Shared.Mappings;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class ContentNodeControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public ContentNodeControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class ContentNodeControllerTests : IClassFixture<ContentNodeControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly IMapper _mapper;

        public ContentNodeControllerTests(ContentNodeControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<GeneratedDocumentProfile>()).CreateMapper();
        }

        [Fact]
        public async Task GetContentNode_WhenNodeExists_ReturnsExpectedNodes()
        {
            // Arrange
            var parentNodeId = Guid.NewGuid();
            var childNodeId = Guid.NewGuid();
            var parentNode = new ContentNode
            {
                Id = parentNodeId,
                Text = "Parent Node",
                ContentNodeSystemItemId = Guid.NewGuid()
            };
            _docGenerationDbContext.ContentNodes.Add(parentNode);
            var childNode = new ContentNode
            {
                Id = childNodeId,
                Text = "Child Node",
                ParentId = parentNodeId,
                ContentNodeSystemItemId = Guid.NewGuid()
            };
            parentNode.Children = new List<ContentNode> { childNode };

            _docGenerationDbContext.ContentNodes.Add(childNode);
            _docGenerationDbContext.SaveChanges();

            var _controller = new ContentNodeController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetContentNode(parentNodeId.ToString());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var contentNodeInfo = Assert.IsType<ContentNodeInfo>(okResult.Value);
            Assert.Equal(parentNode.Id, contentNodeInfo.Id);
            Assert.Equal(parentNode.Text, contentNodeInfo.Text);
            Assert.Equal(parentNode.ContentNodeSystemItemId, contentNodeInfo.ContentNodeSystemItemId);
            Assert.Single(contentNodeInfo.Children);

            var childNodeInfo = contentNodeInfo.Children.First();
            Assert.Equal(childNode.Id, childNodeInfo.Id);
            Assert.Equal(childNode.Text, childNodeInfo.Text);
            Assert.Equal(childNode.ContentNodeSystemItemId, childNodeInfo.ContentNodeSystemItemId);


            // Cleanup
            _docGenerationDbContext.ContentNodes.Remove(parentNode);
            _docGenerationDbContext.ContentNodes.Remove(childNode);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task GetContentNode_WhenNodeNotFound_ReturnsNotFound()
        {
            // Arrange
            var contentNodeId = Guid.NewGuid();
            var _controller = new ContentNodeController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetContentNode(contentNodeId.ToString());

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetContentNodeSystemItem_WhenItemNotFound_ReturnsNotFound()
        {
            // Arrange
            var contentNodeSystemItemId = Guid.NewGuid();
            var _controller = new ContentNodeController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetContentNodeSystemItem(contentNodeSystemItemId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

    }
}
