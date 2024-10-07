namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record GenerateDocumentsDTO
{
    public GenerateDocumentDTO[] Documents { get; set; }
}

public class DocumentGenerationRequest
{
        public string DocumentProcessName { get; set; }
        public string DocumentTitle { get; set; }
        public string AuthorOid { get; set; }
        public string Id { get; set; }
}
