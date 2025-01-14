using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents a reference item information for a document source in kernel memory.
/// </summary>
[JsonDerivedType(typeof(DocumentLibrarySourceReferenceItemInfo), nameof(DocumentLibrarySourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentProcessRepositorySourceReferenceItemInfo), nameof(DocumentProcessRepositorySourceReferenceItemInfo))]
public class KernelMemoryDocumentSourceReferenceItemInfo : SourceReferenceItemInfo
{
    /// <summary>
    /// Index name.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// List of citations.
    /// </summary>
    public List<Citation> Citations { get; set; } = [];

    /// <summary>
    /// Provides a full text output of all the content retrieved.
    /// </summary>
    [Description("Provides a full text output of all the content retrieved.")]
    [JsonIgnore]
    public string FullTextOutput
    {
        get
        {
            var fullText = "";
            foreach (var citation in Citations)
            {
                foreach (var partition in citation.Partitions)
                {
                    fullText += partition.Text;
                }
            }
            return fullText;
        }
    }
}
