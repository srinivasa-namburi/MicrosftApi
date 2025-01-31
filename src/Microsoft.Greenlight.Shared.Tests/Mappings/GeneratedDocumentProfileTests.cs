using System.Text.Json;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.KernelMemory;
using Xunit;
using Assert = Xunit.Assert;


namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class GeneratedDocumentProfileTests
    {
        private const string TITLE = "Title";
        private readonly IMapper _mapper;

        public GeneratedDocumentProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<GeneratedDocumentProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_ContentNode_To_ContentNodeInfo()
        {
            // Arrange
            var contentNode = new ContentNode
            {
                ContentNodeSystemItemId = Guid.NewGuid(),
                Children = [
                    new ContentNode { ContentNodeSystemItemId = Guid.NewGuid() }
                ]
            };

            // Act
            var contentNodeInfo = _mapper.Map<ContentNodeInfo>(contentNode);

            // Assert
            Assert.Equal(contentNode.ContentNodeSystemItemId, contentNodeInfo.ContentNodeSystemItemId);
            Assert.Single(contentNodeInfo.Children);
            Assert.Equal(contentNode.Children.First().ContentNodeSystemItemId, contentNodeInfo.Children.First().ContentNodeSystemItemId);
        }

        [Fact]
        public void Should_Map_ContentNodeInfo_To_ContentNode()
        {
            // Arrange
            var contentNodeInfo = new ContentNodeInfo
            {
                ContentNodeSystemItemId = Guid.NewGuid(),
                Children = [
                    new ContentNodeInfo { ContentNodeSystemItemId = Guid.NewGuid() }
                ]
            };

            // Act
            var contentNode = _mapper.Map<ContentNode>(contentNodeInfo);

            // Assert
            Assert.Equal(contentNodeInfo.ContentNodeSystemItemId, contentNode.ContentNodeSystemItemId);
            Assert.Single(contentNode.Children);
            Assert.Equal(contentNodeInfo.Children.First().ContentNodeSystemItemId, contentNode.Children.First().ContentNodeSystemItemId);
        }

        [Fact]
        public void Should_Map_GeneratedDocument_To_GeneratedDocumentInfo()
        {
            // Arrange
            var generatedDocument = new GeneratedDocument
            {
                ContentNodes = [
                    new ContentNode { ContentNodeSystemItemId = Guid.NewGuid() },
                ],
                Title = TITLE,
            };

            // Act
            var generatedDocumentInfo = _mapper.Map<GeneratedDocumentInfo>(generatedDocument);

            // Assert
            Assert.Single(generatedDocumentInfo.ContentNodes);
            Assert.Equal(generatedDocument.ContentNodes.First().ContentNodeSystemItemId, generatedDocumentInfo.ContentNodes.First().ContentNodeSystemItemId);
        }

        [Fact]
        public void Should_Map_GeneratedDocumentInfo_To_GeneratedDocument()
        {
            // Arrange
            var generatedDocumentInfo = new GeneratedDocumentInfo
            {
                ContentNodes = [
                    new ContentNodeInfo { ContentNodeSystemItemId = Guid.NewGuid() }
                ],

                Title = TITLE,
            };

            // Act
            var generatedDocument = _mapper.Map<GeneratedDocument>(generatedDocumentInfo);

            // Assert
            Assert.Single(generatedDocument.ContentNodes);
            Assert.Equal(generatedDocumentInfo.ContentNodes.First().ContentNodeSystemItemId, generatedDocument.ContentNodes.First().ContentNodeSystemItemId);
        }

        [Fact]
        public void Should_Map_KernelMemoryDocumentSourceReferenceItem_To_KernelMemoryDocumentSourceReferenceItemInfo()
        {
            // Arrange
            const string SOURCE_NAME = "Source Name";
            var kernelMemoryDocumentSourceReferenceItem = new KernelMemoryDocumentSourceReferenceItemImplementation
            {
                CitationJsons = [
                    JsonSerializer.Serialize(new Citation {
                        SourceName = SOURCE_NAME
                    })
                ]
            };

            // Act
            var kernelMemoryDocumentSourceReferenceItemInfo = _mapper.Map<KernelMemoryDocumentSourceReferenceItemInfo>(kernelMemoryDocumentSourceReferenceItem);

            // Assert
            Assert.Single(kernelMemoryDocumentSourceReferenceItemInfo.Citations);
            Assert.Equal(
                SOURCE_NAME, 
                kernelMemoryDocumentSourceReferenceItemInfo.Citations.First().SourceName);
        }
    }

    public class KernelMemoryDocumentSourceReferenceItemImplementation : KernelMemoryDocumentSourceReferenceItem
    {
        public override string? SourceOutput { get; set; }
        public override void SetBasicParameters() { }
    }
}
