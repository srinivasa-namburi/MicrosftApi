using System.ComponentModel;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

/// <summary>
/// Represents the usage information of a document library.
/// </summary>
public class DocumentLibraryUsageInfo
{
    /// <summary>
    /// Unique identifier of the document library.
    /// </summary>
    [Description("The unique identifier of the document library")]
    public required Guid Id { get; set; }

    /// <summary>
    /// Short name of the document library.
    /// </summary>
    [Description("The short name of the document library")]
    public required string DocumentLibraryShortName { get; set; }

    /// <summary>
    /// Name of the index underpinning the document library.
    /// </summary>
    [Description("The name of the index underpinning the document library")]
    public required string IndexName { get; set; }

    /// <summary>
    /// Description of the types of information and documents present in this document library.
    /// </summary>
    [Description("A description of the types of information and documents present in this document library")]
    public required string DescriptionOfContents { get; set; }

    /// <summary>
    /// Description of when to use this document library.
    /// </summary>
    [Description("A description of when to use this document library")]
    public required string DescriptionOfWhenToUse { get; set; }
}
