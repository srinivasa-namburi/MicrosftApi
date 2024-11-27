using System.ComponentModel;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

public class DocumentLibraryUsageInfo
{
    [Description("The unique identifier of the document library")]
    public required Guid Id { get; set; }
    [Description("The short name of the document library")]
    public required string DocumentLibraryShortName { get; set; }
    [Description("The name of the index underpinning the document library")]
    public required string IndexName { get; set; }
    [Description("A description of the types of information and documents present in this document library")]
    public required string DescriptionOfContents { get; set; }
    [Description("A description of when to use this document library")]
    public required string DescriptionOfWhenToUse { get; set; }
}