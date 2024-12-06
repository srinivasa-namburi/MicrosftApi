using System.ComponentModel;
using System.Text.Json;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

public abstract class KernelMemoryDocumentSourceReferenceItem : SourceReferenceItem
{
    public string? IndexName { get; set; }
    public List<string> CitationJsons { get; set; } = [];

    [Description("Provides a full text output of all the content retrieved.")]
    public string FullTextOutput
    {
        get
        {
            var fullText = "";
            var documentCitations = GetCitations();
            foreach (var citation in documentCitations)
            {
                foreach (var partition in citation.Partitions)
                {
                    fullText += partition.Text;
                }
            }
            return fullText;
        }
    }
    [Description("Provides a JSON output of the internal structure of the content. Not to be output directly to prompt or end user.")]
    public override string? SourceOutput
    {
        get => JsonSerializer.Serialize(GetCitations());
        set => AddCitations(JsonSerializer.Deserialize<List<Citation>>(value));
    }

    public override void SetBasicParameters()
    {
        SourceReferenceLinkType = Enums.SourceReferenceLinkType.SystemNonProxiedUrl;
        Description = "Document fragments from Kernel Memory document source";
    }

    public void AddCitations(ICollection<Citation> citations)
    {
        // Serialize the citations to JSON and store them in the CitationJsons property
        foreach (var citation in citations)
        {
            AddCitation(citation);
        }
    }

    public void AddCitation(Citation citation)
    {
        // Serialize the citation to JSON and store it in the CitationJsons property
        var jsonCitation = JsonSerializer.Serialize(citation);
        CitationJsons.Add(jsonCitation);
    }

    public List<Citation> GetCitations()
    {
        // Deserialize the citations from JSON and return them
        var citations = new List<Citation>();
        foreach (var jsonCitation in CitationJsons)
        {
            var citation = JsonSerializer.Deserialize<Citation>(jsonCitation);
            if (citation != null)
            {
                citations.Add(citation);
            }
        }
        return citations;
    }
}