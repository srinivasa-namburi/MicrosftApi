using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class DocumentLibraryMappingProfileTests
    {
        private readonly IMapper _mapper;

        public DocumentLibraryMappingProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<DocumentLibraryMappingProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_DocumentLibraryDocumentProcessAssociation_To_DocumentLibraryDocumentProcessAssociationInfo()
        {
            // Arrange
            var documentLibraryDocumentProcessAssociation = new DocumentLibraryDocumentProcessAssociation
            {
                DynamicDocumentProcessDefinition = new DynamicDocumentProcessDefinition { 
                    ShortName = "TestShortName",
                    Description = "TestDescription",
                    BlobStorageAutoImportFolderName = "TestBlobStorageAutoImportFolderName",
                    BlobStorageContainerName = "TestBlobStorageContainerName"
                },
                DynamicDocumentProcessDefinitionId = Guid.NewGuid(),
                DocumentLibraryId = Guid.NewGuid()
            };

            // Act
            var documentLibraryDocumentProcessAssociationInfo = _mapper.Map<DocumentLibraryDocumentProcessAssociationInfo>(documentLibraryDocumentProcessAssociation);

            // Assert
            Assert.NotNull(documentLibraryDocumentProcessAssociationInfo);
            Assert.Equal(
                documentLibraryDocumentProcessAssociation.DynamicDocumentProcessDefinition.ShortName, 
                documentLibraryDocumentProcessAssociationInfo.DocumentProcessShortName);
        }

        [Fact]
        public void Should_Map_DocumentLibraryInfo_To_DocumentLibraryUsageInfo()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { 
                ShortName = "TestShortName"
            };

            // Act
            var documentLibraryUsageInfo = _mapper.Map<DocumentLibraryUsageInfo>(documentLibraryInfo);

            // Assert
            Assert.NotNull(documentLibraryUsageInfo);
            Assert.Equal(
                documentLibraryInfo.ShortName, 
                documentLibraryUsageInfo.DocumentLibraryShortName);
        }

        [Fact]
        public void Should_Map_DocumentLibrary_To_DocumentLibraryUsageInfo()
        {
            // Arrange 
            var documentLibrary = new DocumentLibrary
            {
                ShortName = "TestShortName",
                DescriptionOfContents = "TestDescriptionOfContents",
                DescriptionOfWhenToUse = "TestDescriptionOfWhenToUse",
                IndexName = "TestIndexName",
                BlobStorageContainerName = "TestBlobStorageContainerName",
                BlobStorageAutoImportFolderName = "TestBlobStorageAutoImportFolderName",
                DocumentProcessAssociations = []
            };

            // Act
            var documentLibraryUsageInfo = _mapper.Map<DocumentLibraryUsageInfo>(documentLibrary);

            // Assert
            Assert.NotNull(documentLibraryUsageInfo);
            Assert.Equal(
                documentLibrary.ShortName, 
                documentLibraryUsageInfo.DocumentLibraryShortName);
        }
    }
}
