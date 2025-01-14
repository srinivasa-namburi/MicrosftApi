namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request to store a document for a review instance from blob storage.
/// </summary>
public class StoreDocumentForReviewInstanceFromBlobRequest
{
    /// <summary>
    /// Review instance ID.
    /// </summary>
    public Guid ReviewInstanceId { get; set; }

    /// <summary>
    /// Full URL of the blob.
    /// </summary>
    public string FullBlobUrl { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }
}
