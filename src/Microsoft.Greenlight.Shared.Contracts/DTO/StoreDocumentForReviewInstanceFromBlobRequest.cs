namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public class StoreDocumentForReviewInstanceFromBlobRequest
{
    public Guid ReviewInstanceId { get; set; }
    public string FullBlobUrl { get; set; }
    public string FileName { get; set; }
}
