using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;
using static Microsoft.KernelMemory.Citation;

namespace Microsoft.Greenlight.Shared.Models.Tests.SourceReferences
{
    public class KernelMemoryDocumentSourceReferenceItemTest
    {
        private class TestKernelMemoryDocumentSourceReferenceItem : KernelMemoryDocumentSourceReferenceItem
        {
            public override void SetBasicParameters()
            {
                // Implementation not needed for tests
            }
        }

        [Fact]
        public void FullTextOutput_WithMultiplePartitions_ReturnConcatenatedText()
        {
            // Arrange
            var item = new TestKernelMemoryDocumentSourceReferenceItem();
            var citation = new Citation
            {
                Partitions =
                [
                    new Partition { Text = "Text1" },
                    new Partition { Text = "Text2" }
                ]
            };
            item.AddCitation(citation);

            // Act
            var result = item.FullTextOutput;

            // Assert
            Assert.Equal("Text1Text2", result);
        }

        [Fact]
        public void GetHighestScoringPartitionFromCitations_WithMultiplePartitions_ReturnHighestScore()
        {
            // Arrange
            var item = new TestKernelMemoryDocumentSourceReferenceItem();
            var citation = new Citation
            {
                Partitions =
                [
                    new Partition { Relevance = 0.5F },
                    new Partition { Relevance = 0.8F }
                ]
            };
            item.AddCitation(citation);

            // Act
            var result = item.GetHighestScoringPartitionFromCitations();

            // Assert
            Assert.Equal(0.8F, result);
        }
    }
}
