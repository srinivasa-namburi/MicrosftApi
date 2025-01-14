using System.ComponentModel;
using System.Text.Json;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a reference item for Kernel Memory documents.
/// </summary>
public abstract class KernelMemoryDocumentSourceReferenceItem : SourceReferenceItem
{
    /// <summary>
    /// Index name within the kernel memory where documents are stored.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// List of citation JSON strings.
    /// </summary>
    public List<string> CitationJsons { get; set; } = [];

    /// <summary>
    /// Provides a full text output of all the content retrieved.
    /// </summary>
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

    /// <summary>
    /// Provides a JSON output of the internal structure of the content. 
    /// Not to be output directly to prompt or end user.
    /// </summary>
    [Description("Provides a JSON output of the internal structure of the content. Not to be output directly to prompt or end user.")]
    public override string? SourceOutput
    {
        get => JsonSerializer.Serialize(GetCitations());
        set
        {
            if (value != null)
            {
                var citations = JsonSerializer.Deserialize<List<Citation>>(value);
                if (citations != null)
                {
                    AddCitations(citations);
                }
            }
        }
    }

    /// <summary>
    /// Sets the basic parameters for the source reference item.
    /// </summary>
    public override void SetBasicParameters()
    {
        SourceReferenceLinkType = Enums.SourceReferenceLinkType.SystemNonProxiedUrl;
        Description = "Document fragments from Kernel Memory document source";
    }

    /// <summary>
    /// Adds a collection of citations to the reference item.
    /// </summary>
    /// <param name="citations">The collection of citations to add.</param>
    public void AddCitations(ICollection<Citation> citations)
    {
        foreach (var citation in citations)
        {
            AddCitation(citation);
        }
    }

    /// <summary>
    /// Adds a single citation to the reference item.
    /// </summary>
    /// <param name="citation">The citation to add.</param>
    public void AddCitation(Citation citation)
    {
        string jsonCitation;
        try
        {
            jsonCitation = JsonSerializer.Serialize(citation);
        }
        catch (Exception)
        {
            return;
        }

        CitationJsons.Add(jsonCitation);
    }

    /// <summary>
    /// Gets the list of citations from the reference item.
    /// </summary>
    /// <returns>The list of citations.</returns>
    public List<Citation> GetCitations()
    {
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

    /// <summary>
    /// Gets the highest scoring partition from the citations.
    /// </summary>
    /// <returns>The highest relevance score.</returns>
    public double GetHighestScoringPartitionFromCitations()
    {
        var citations = GetCitations();

        float highestScore = 0.0F;
        foreach (var citation in citations)
        {
            foreach (var partition in citation.Partitions)
            {
                if (partition.Relevance > highestScore)
                {
                    highestScore = partition.Relevance;
                }
            }
        }
        return highestScore;
    }
}
