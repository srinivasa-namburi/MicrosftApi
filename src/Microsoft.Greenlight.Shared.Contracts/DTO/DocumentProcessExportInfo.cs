namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents information about the document process export.
/// </summary>
public record DocumentProcessExportInfo
{
    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public string DocumentProcessShortName { get; set; }

    /// <summary>
    /// Description of the document process.
    /// </summary>
    public string DocumentProcessDescription { get; set; }

    /// <summary>
    /// Prompts associated with the document process.
    /// </summary>
    public string Prompts { get; set; }

    /// <summary>
    /// Prompt definitions associated with the document process.
    /// </summary>
    public string PromptDefinitions { get; set; }
}
