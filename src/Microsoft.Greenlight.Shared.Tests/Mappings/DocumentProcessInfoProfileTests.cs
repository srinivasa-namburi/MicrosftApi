using AutoMapper;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Mappings;
using Xunit;
using Assert = Xunit.Assert;


namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class DocumentProcessInfoProfileTests
    {
        private readonly IMapper _mapper;

        public DocumentProcessInfoProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<DocumentProcessInfoProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_DocumentProcessOptions_To_DocumentProcessInfo()
        {
            // Arrange 
            var options = new DocumentProcessOptions
            {
                Name = "TestName",
                Repositories = ["Repo1", "Repo2"],
                IngestionMethod = "Classic"
            };

            // Act
            var result = _mapper.Map<DocumentProcessInfo>(options);

            // Assert
            Assert.Equal(Guid.Empty, result.Id);
            Assert.Equal(options.Name, result.ShortName);
            Assert.Equal(string.Empty, result.Description);
            Assert.Equal(string.Empty, result.OutlineText);
            Assert.Equal(options.Repositories, result.Repositories);
            Assert.Equal(DocumentProcessLogicType.Classic, result.LogicType);
        }

        [Fact]
        public void Should_Map_DocumentProcessOptions_To_DocumentProcessInfo_DefaultsToKernalMemory()
        {
            // Arrange
            var options = new DocumentProcessOptions
            {
                Name = "TestName",
                IngestionMethod = null
            };

            // Act
            var result = _mapper.Map<DocumentProcessInfo>(options);

            // Assert
            Assert.Equal(DocumentProcessLogicType.KernelMemory, result.LogicType);
        }

        [Fact]
        public void Should_Map_DocumentProcessInfo_To_DynamicDocumentProcessDefinition()
        {
            // Arrange
            var info = new DocumentProcessInfo
            {
                LogicType = DocumentProcessLogicType.KernelMemory,
                CompletionServiceType = DocumentProcessCompletionServiceType.AgentAiCompletionService,
                Repositories = ["Repo1", "Repo2"]
            };

            // Act
            var result = _mapper.Map<DynamicDocumentProcessDefinition>(info);

            // Assert
            Assert.Equal(info.LogicType, result.LogicType);
            Assert.Equal(info.CompletionServiceType, result.CompletionServiceType);
            Assert.Equal(info.Repositories, result.Repositories);
        }

        [Fact]
        public void Should_Map_DynamicDocumentProcessDefinition_To_DocumentProcessInfo()
        {
            // Arrange
            var definition = new DynamicDocumentProcessDefinition
            {
                DocumentOutlineId = Guid.NewGuid(),
                ShortName = "TestShortName",
                BlobStorageContainerName = "TestContainer",
                BlobStorageAutoImportFolderName = "TestFolder",
                Repositories = ["Repo1", "Repo2"]
            };

            // Act
            var result = _mapper.Map<DocumentProcessInfo>(definition);

            // Assert
            Assert.Equal(definition.Repositories, result.Repositories);
            Assert.Null(result.OutlineText);
            Assert.Equal(definition.DocumentOutlineId, result.DocumentOutlineId);
        }

        [Fact]
        public void Should_Map_DynamicDocumentProcessDefinition_To_DocumentProcessInfo_UsesDocumentOutlineChildProperty()
        {
            // Arrange
            var definition = new DynamicDocumentProcessDefinition
            {
                ShortName = "TestShortName",
                BlobStorageContainerName = "TestContainer",
                BlobStorageAutoImportFolderName = "TestFolder",
                Repositories = ["Repo1", "Repo2"],
                DocumentOutline = new DocumentOutline { FullText = "0 Outline Text" }
            };

            // Act
            var result = _mapper.Map<DocumentProcessInfo>(definition);

            // Assert
            Assert.Equal(definition.Repositories, result.Repositories);
            Assert.Equal("0 Outline Text\n", result.OutlineText);
            Assert.Equal(definition.DocumentOutline.Id, result.DocumentOutlineId);
        }
    }
}
