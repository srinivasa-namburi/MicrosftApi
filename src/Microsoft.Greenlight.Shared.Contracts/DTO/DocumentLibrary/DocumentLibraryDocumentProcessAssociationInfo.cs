namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

public class DocumentLibraryDocumentProcessAssociationInfo
{
    public Guid Id { get; set; }
    public Guid DocumentLibraryId { get; set; }
    public Guid DynamicDocumentProcessDefinitionId { get; set; }
    public required string DocumentProcessShortName { get; set; }
}