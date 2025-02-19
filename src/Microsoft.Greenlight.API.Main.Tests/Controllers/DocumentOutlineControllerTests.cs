using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class DocumentOutlineControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public DocumentOutlineControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class DocumentOutlineControllerTests : IClassFixture<DocumentOutlineControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly DbContextOptions<DocGenerationDbContext> _fakeDbContextOptions = new();
        private readonly Mock<DocGenerationDbContext> _mockDocGenerationDbContext;
        private readonly IMapper _mapper;

        public DocumentOutlineControllerTests(DocumentOutlineControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mockDocGenerationDbContext = new Mock<DocGenerationDbContext>(_fakeDbContextOptions);
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<DocumentOutlineInfoProfile>()).CreateMapper();
        }

        [Fact]
        public async Task GetAllDocumentOutlines_WhenNoDocumentOutlinesExist_ReturnsNotFound()
        {
            // Arrange
            var _controller = new DocumentOutlineController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetAllDocumentOutlines();

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetDocumentOutlineById_WhenDocumentOutlineDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var _controller = new DocumentOutlineController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetDocumentOutlineById(nonExistentId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateDocumentOutline_WhenDocumentOutlineDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var changeRequest = new DocumentOutlineChangeRequest
            {
                DocumentOutlineInfo = new DocumentOutlineInfo { Id = Guid.NewGuid() }
            }; 
            var _controller = new DocumentOutlineController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateDocumentOutline(Guid.NewGuid(), changeRequest);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateDocumentOutline_WhenExistingItemIsUpdated_UpdatesDatabaseCorrectly()
        {
            // Arrange
            var testShortName = "UniqueShortName1";
            var testBlobStorageContainerName = "TestContainerName";
            var testBlobStorageAutoImportFolderName = "TestAutoImportFolderName";
            var testSectionTitle = "Test Section Title";
            var testChangedSectionTitle = "Updated Section Title";

            var documentProcessDefinition = new DynamicDocumentProcessDefinition
            {
                Id = Guid.NewGuid(),
                ShortName = testShortName, 
                BlobStorageContainerName = testBlobStorageContainerName,
                BlobStorageAutoImportFolderName = testBlobStorageAutoImportFolderName
            }; 
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Add(documentProcessDefinition);

            var documentOutline = new DocumentOutline
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = documentProcessDefinition.Id
            };
           _docGenerationDbContext.DocumentOutlines.Add(documentOutline);

            var documentOutlineItem = new DocumentOutlineItem
            {
                Id = Guid.NewGuid(),
                SectionTitle = testSectionTitle,
                Level = 1
            };
            _docGenerationDbContext.DocumentOutlineItems.Add(documentOutlineItem);
            _docGenerationDbContext.SaveChanges();

            var changeRequest = new DocumentOutlineChangeRequest
            {
                DocumentOutlineInfo = new DocumentOutlineInfo
                {
                    Id = documentOutline.Id,
                    DocumentProcessDefinitionId = documentProcessDefinition.Id
                },
                ChangedOutlineItems =
                [
                    new DocumentOutlineItemInfo
                    {
                        Id = documentOutlineItem.Id,
                        SectionTitle = testChangedSectionTitle,
                        Level = 2
                    }
                ]
            };
            var _controller = new DocumentOutlineController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateDocumentOutline(documentOutline.Id, changeRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<DocumentOutlineInfo>(okResult.Value);
            Assert.Equal(documentOutline.Id, returnValue.Id);
            Assert.Equal(documentOutline.DocumentProcessDefinitionId, returnValue.DocumentProcessDefinitionId);

            Assert.Contains(_docGenerationDbContext.DocumentOutlineItems, items => items.SectionTitle == testChangedSectionTitle);
            Assert.Contains(_docGenerationDbContext.DocumentOutlineItems, items => items.Level == 2);
            Assert.Contains(_docGenerationDbContext.DocumentOutlineItems, items => items.Id == documentOutlineItem.Id);

            // Cleanup
            _docGenerationDbContext.DynamicDocumentProcessDefinitions.Remove(documentProcessDefinition);
            _docGenerationDbContext.DocumentOutlines.Remove(documentOutline);
            _docGenerationDbContext.DocumentOutlineItems.Remove(documentOutlineItem);
            _docGenerationDbContext.SaveChanges();
        }


        [Fact]
        public void GenerateOutlineFromText_WhenTextIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var textDto = new SimpleTextDTO { Text = "" };
            var _controller = new DocumentOutlineController(_mockDocGenerationDbContext.Object, _mapper);

            // Act
            var result = _controller.GenerateOutlineFromText(textDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
