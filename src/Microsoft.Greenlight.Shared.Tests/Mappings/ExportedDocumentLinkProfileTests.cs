using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Mappings;
using Xunit;
using Assert = Xunit.Assert;


namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class ExportedDocumentLinkProfileTests
    {
        private readonly IMapper _mapper;

        public ExportedDocumentLinkProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<ExportedDocumentLinkProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_ExportedDocumentLink_To_ExportedDocumentLinkInfo()
        {
            // Arrange
            var exportedDocumentLink = new ExportedDocumentLink
            {
                Id = Guid.NewGuid(),
                GeneratedDocument = new GeneratedDocument
                {
                    Id = Guid.NewGuid(),
                    Title = "Title",
                },
                MimeType = "text/html",
                AbsoluteUrl = "AbsoluteUrl",
                BlobContainer = "Blob Container",
                FileName = "File Name"
            };

            // Act
            var exportedDocumentLinkInfo = _mapper.Map<ExportedDocumentLinkInfo>(exportedDocumentLink);

            // Assert
            Assert.Equal(exportedDocumentLink.Id, exportedDocumentLinkInfo.Id);
            Assert.Equal(exportedDocumentLink.GeneratedDocument.Id, exportedDocumentLinkInfo.GeneratedDocumentId);
        }

        [Fact]
        public void Should_Map_ExportedDocumentLink_To_ExportedDocumentLinkInfo_UsesIdWhenObjectNotPresent()
        {
            // Arrange
            var exportedDocumentLink = new ExportedDocumentLink
            {
                Id = Guid.NewGuid(),
                GeneratedDocumentId = Guid.NewGuid(),
                MimeType = "text/html",
                AbsoluteUrl = "AbsoluteUrl",
                BlobContainer = "Blob Container",
                FileName = "File Name"
            };

            // Act
            var exportedDocumentLinkInfo = _mapper.Map<ExportedDocumentLinkInfo>(exportedDocumentLink);

            // Assert
            Assert.Equal(exportedDocumentLink.Id, exportedDocumentLinkInfo.Id);
            Assert.Equal(exportedDocumentLink.GeneratedDocumentId, exportedDocumentLinkInfo.GeneratedDocumentId);
        }

        [Fact]
        public void Should_Map_ExportedDocumentLinkInfo_To_ExportedDocumentLink()
        {
            // Arrange
            var exportedDocumentLinkInfo = new ExportedDocumentLinkInfo
            {
                Id = Guid.NewGuid(),
                GeneratedDocumentId = Guid.NewGuid(),
                MimeType = "text/html",
                AbsoluteUrl = "AbsoluteUrl",
                BlobContainer = "Blob Container",
                FileName = "File Name"
            };

            // Act
            var exportedDocumentLink = _mapper.Map<ExportedDocumentLink>(exportedDocumentLinkInfo);

            // Assert
            Assert.Equal(exportedDocumentLinkInfo.Id, exportedDocumentLink.Id);
            Assert.Equal(exportedDocumentLinkInfo.GeneratedDocumentId, exportedDocumentLink.GeneratedDocumentId);
        }
    }
}
