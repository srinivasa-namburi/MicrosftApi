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

        [Fact]
        public void Should_Map_VectorStoreAggregatedSourceReferenceItem_To_VectorStoreSourceReferenceItemInfo()
        {
            // Arrange
            var aggregated = new VectorStoreAggregatedSourceReferenceItem
            {
                IndexName = "libindex",
                DocumentId = "doc123",
                FileName = "file.txt",
                Score = 0.92,
            };
            aggregated.SetBasicParameters();
            aggregated.Chunks.Add(new DocumentChunk
            {
                Text = "First chunk text",
                Relevance = 0.91,
                PartitionNumber = 0,
                SizeInBytes = 17,
                Tags = new Dictionary<string, List<string?>>
                {
                    ["DocumentId"] = ["doc123"],
                    ["FileName"] = ["file.txt"],
                },
                LastUpdate = DateTimeOffset.UtcNow
            });
            aggregated.Chunks.Add(new DocumentChunk
            {
                Text = "Second chunk text",
                Relevance = 0.85,
                PartitionNumber = 1,
                SizeInBytes = 18,
                Tags = new Dictionary<string, List<string?>>
                {
                    ["DocumentId"] = ["doc123"],
                    ["FileName"] = ["file.txt"],
                },
                LastUpdate = DateTimeOffset.UtcNow
            });

            // Act
            var dto = _mapper.Map<VectorStoreSourceReferenceItemInfo>(aggregated);

            // Assert
            Assert.Equal(aggregated.IndexName, dto.IndexName);
            Assert.Equal(aggregated.DocumentId, dto.DocumentId);
            Assert.Equal(aggregated.FileName, dto.FileName);
            Assert.Equal(aggregated.Score, dto.Score);
            Assert.Equal(aggregated.Chunks.Count, dto.Chunks.Count);
            Assert.Equal(aggregated.Chunks[0].Text, dto.Chunks[0].Text);
            Assert.Equal(aggregated.Chunks[0].PartitionNumber, dto.Chunks[0].PartitionNumber);
            Assert.Equal(aggregated.Chunks[1].Relevance, dto.Chunks[1].Relevance);
        }
    }

    public class KernelMemoryDocumentSourceReferenceItemImplementation : KernelMemoryDocumentSourceReferenceItem
    {
        public override string? SourceOutput { get; set; }
        public override void SetBasicParameters() { }
    }
}
