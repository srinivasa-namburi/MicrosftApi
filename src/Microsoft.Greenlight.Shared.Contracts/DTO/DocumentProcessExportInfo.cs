namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record DocumentProcessExportInfo
{
    public string DocumentProcessShortName { get; set; }
    public string DocumentProcessDescription { get; set; }
    public string Prompts { get; set; }
    public string PromptDefinitions { get; set; }
}
