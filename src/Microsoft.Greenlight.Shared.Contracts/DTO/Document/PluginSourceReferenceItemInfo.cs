namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

public class PluginSourceReferenceItemInfo : SourceReferenceItemInfo
{
    public string? PluginIdentifier { get; set; }
    public string? SourceInputJson { get; set; }
    public bool SourceOutputIsValidJson { get; set; }
}