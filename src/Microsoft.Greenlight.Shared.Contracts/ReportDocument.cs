namespace Microsoft.Greenlight.Shared.Contracts;

/// <summary>
/// Represents a report document.
/// </summary>
public class ReportDocument
{
    /// <summary>
    /// Unique identifier of the report document.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Title of the report document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Type of the report document.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Identifier of the parent document, if any.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Title of the parent document, if any.
    /// </summary>
    public string? ParentTitle { get; set; }

    /// <summary>
    /// Original file name of the report document.
    /// </summary>
    public string OriginalFileName { get; set; }

    /// <summary>
    /// Hash of the original file of the report document.
    /// </summary>
    public string OriginalFileHash { get; set; }

    /// <summary>
    /// Content of the report document.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Vector representation of the title.
    /// </summary>
    public float[] TitleVector { get; set; }

    /// <summary>
    /// Vector representation of the content.
    /// </summary>
    public float[] ContentVector { get; set; }
}
