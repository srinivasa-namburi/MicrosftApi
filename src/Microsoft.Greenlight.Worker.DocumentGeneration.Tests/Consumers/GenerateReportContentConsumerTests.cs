using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Moq;
using Xunit;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Tests
{
    public class GenerateReportContentConsumerTests
    {
        private readonly DocGenerationDbContext _fakeDbContext =
            new Mock<DocGenerationDbContext>(new DbContextOptionsBuilder<DocGenerationDbContext>().Options).Object;
        private readonly ILogger<GenerateReportContentConsumer> _fakeLogger =
            new Mock<ILogger<GenerateReportContentConsumer>>().Object;

        // Cannot currently have a "Title Only" node because of DbContext.
        private readonly string _sixNodesJson = @"
        {
            ""Title"": ""Test Title"",
            ""GeneratedDate"": ""2023-10-01T00:00:00Z"",
            ""RequestingAuthorOid"": ""00000000-0000-0000-0000-000000000000"",
            ""ContentNodes"": [
                {
                    ""NodeType"": ""Title"",
                    ""Content"": ""Test Title2 Content"",
                    ""Children"": [
                        {
                            ""NodeType"": ""Heading"",
                            ""Content"": ""Test Heading1 Content"",
                            ""Children"": [
                                {
                                    ""NodeType"": ""BodyText"",
                                    ""Content"": ""Test Paragraph3 Content"",
                                    ""Children"": []
                                }
                            ]
                        },
                        {
                            ""NodeType"": ""Heading"",
                            ""Content"": ""Test Heading2 Content"",
                            ""Children"": [
                                {
                                    ""NodeType"": ""BodyText"",
                                    ""Content"": ""Test Paragraph2 Content"",
                                    ""Children"": []
                                },
                                {
                                    ""NodeType"": ""BodyText"",
                                    ""Content"": ""Test Paragraph3 Content"",
                                    ""Children"": []
                                }
                            ]
                        }   
                    ]
                }
            ]
        }";

        [Fact]
        public async void Consume_WithSixNodes_PublishesGenerateReportTitleSectionSixTimes()
        {
            // Arrange
            var mockMessage = new Mock<GenerateReportContent>(Guid.NewGuid());
            mockMessage.SetupGet(m => m.GeneratedDocumentJson).Returns(_sixNodesJson);
            var fakeContext = new Mock<ConsumeContext<GenerateReportContent>>();
            fakeContext.Setup(ctx => ctx.Message).Returns(mockMessage.Object);
            var unitUnderTest = new GenerateReportContentConsumer(_fakeLogger, _fakeDbContext);

            // Act
            await unitUnderTest.Consume(fakeContext.Object);

            // Assert
            fakeContext.Verify(ctx => ctx.Publish(It.IsAny<GenerateReportTitleSection>(), default), Times.Exactly(6));
        }

        [Fact]
        public async void Consume_WithoutTitleOnly_DoesNotPublishContentNodeGenerated()
        {
            // Arrange
            var mockMessage = new Mock<GenerateReportContent>(Guid.NewGuid());
            mockMessage.SetupGet(m => m.GeneratedDocumentJson).Returns(_sixNodesJson);
            var fakeContext = new Mock<ConsumeContext<GenerateReportContent>>();
            fakeContext.Setup(ctx => ctx.Message).Returns(mockMessage.Object);
            var unitUnderTest = new GenerateReportContentConsumer(_fakeLogger, _fakeDbContext);

            // Act
            await unitUnderTest.Consume(fakeContext.Object);

            // Assert
            fakeContext.Verify(ctx => ctx.Publish(It.IsAny<ContentNodeGenerated>(), default), Times.Never);
        }
    }
}