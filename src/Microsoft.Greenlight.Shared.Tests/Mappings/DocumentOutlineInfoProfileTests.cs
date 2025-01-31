using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Mappings;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class DocumentOutlineInfoProfileTests
    {
        private const string SECTION_2 = "Section 2";
        private const string SECTION_1 = "Section 1";
        private const string SECTION_1_2 = "Section 1.2";
        private const string SECTION_1_1 = "Section 1.1";
        private readonly IMapper _mapper;

        public DocumentOutlineInfoProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<DocumentOutlineInfoProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_DocumentOutline_To_DocumentOutlineInfo_InCorrectOrder()
        {
            // Arrange
            var documentOutline = new DocumentOutline
            {
                OutlineItems =
                    [
                    new DocumentOutlineItem
                        {
                            SectionTitle = SECTION_2,
                            Level = 1,
                            OrderIndex = 2
                        },
                        new DocumentOutlineItem
                        {
                            SectionTitle = SECTION_1,
                            Level = 1,
                            OrderIndex = 1,
                            Children =
                            [
                                new DocumentOutlineItem
                                {
                                    SectionTitle = SECTION_1_2,
                                    Level = 2,
                                    OrderIndex = 2
                                },
                                new DocumentOutlineItem
                                {
                                    SectionTitle = SECTION_1_1,
                                    Level = 2,
                                    OrderIndex = 1
                                }
                            ]
                        }                        
                    ]
            };

            // Act
            var documentOutlineInfo = _mapper.Map<DocumentOutlineInfo>(documentOutline);

            // Assert
            Assert.NotNull(documentOutlineInfo);
            Assert.Equal(2, documentOutlineInfo.OutlineItems.Count);
            Assert.Equal(SECTION_1, documentOutlineInfo.OutlineItems[0].SectionTitle);
            Assert.Equal(2, documentOutlineInfo.OutlineItems[0].Children.Count);
            Assert.Equal(SECTION_1_1, documentOutlineInfo.OutlineItems[0].Children[0].SectionTitle);
            Assert.Equal(SECTION_1_2, documentOutlineInfo.OutlineItems[0].Children[1].SectionTitle);
            Assert.Equal(SECTION_2, documentOutlineInfo.OutlineItems[1].SectionTitle);
        }

        [Fact]
        public void Should_Map_DocumentOutlineInfo_To_DocumentOutline_InCorrectOrder()
        {
            // Arrange
            var documentOutlineInfo = new DocumentOutlineInfo
            {
                OutlineItems =
                    [
                        new DocumentOutlineItemInfo
                        {
                            SectionTitle = SECTION_2,
                            Level = 1,
                            OrderIndex = 2
                        },
                        new DocumentOutlineItemInfo
                        {
                            SectionTitle = SECTION_1,
                            Level = 1,
                            OrderIndex = 1,
                            Children =
                            [
                                new DocumentOutlineItemInfo
                                {
                                    SectionTitle = SECTION_1_2,
                                    Level = 2,
                                    OrderIndex = 2
                                },
                                new DocumentOutlineItemInfo
                                {
                                    SectionTitle = SECTION_1_1,
                                    Level = 2,
                                    OrderIndex = 1
                                }
                            ]
                        }
                        
                    ]
            };

            // Act
            var documentOutline = _mapper.Map<DocumentOutline>(documentOutlineInfo);

            // Assert
            Assert.NotNull(documentOutline);
            Assert.Equal(2, documentOutline.OutlineItems.Count);
            Assert.Equal(SECTION_1, documentOutline.OutlineItems[0].SectionTitle);
            Assert.Equal(2, documentOutline.OutlineItems[0].Children.Count);
            Assert.Equal(SECTION_1_1, documentOutline.OutlineItems[0].Children[0].SectionTitle);
            Assert.Equal(SECTION_1_2, documentOutline.OutlineItems[0].Children[1].SectionTitle);
            Assert.Equal(SECTION_2, documentOutline.OutlineItems[1].SectionTitle);
        }
    }
}
