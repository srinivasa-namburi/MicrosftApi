namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request to store a document for a review instance.
/// </summary>
public class StoreDocumentForReviewInstanceRequest
{
    /// <summary>
    /// Unique identifier for the review instance.
    /// </summary>
    public Guid ReviewInstanceId { get; set; }

    /// <summary>
    /// File stream of the document.
    /// </summary>
    public Stream FileStream { get; set; }

    /// <summary>
    /// Name of the file.
    /// </summary>
    public string FileName { get; set; }
}
