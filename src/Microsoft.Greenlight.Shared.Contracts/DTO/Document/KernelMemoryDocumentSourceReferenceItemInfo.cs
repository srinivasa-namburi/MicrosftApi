using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

[JsonDerivedType(typeof(DocumentLibrarySourceReferenceItemInfo),nameof(DocumentLibrarySourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentProcessRepositorySourceReferenceItemInfo), nameof(DocumentProcessRepositorySourceReferenceItemInfo))]
public class KernelMemoryDocumentSourceReferenceItemInfo : SourceReferenceItemInfo
{
    public string? IndexName { get; set; }
    public List<Citation> Citations { get; set; } = [];

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