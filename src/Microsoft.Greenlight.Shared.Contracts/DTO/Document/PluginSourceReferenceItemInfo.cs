namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a plugin source reference item.
/// </summary>
public class PluginSourceReferenceItemInfo : SourceReferenceItemInfo
{
    /// <summary>
    /// Identifier of the plugin.
    /// </summary>
    public string? PluginIdentifier { get; set; }

    /// <summary>
    /// Source input JSON.
    /// </summary>
    public string? SourceInputJson { get; set; }

    /// <summary>
    /// Value indicating whether the source output is valid JSON.
    /// </summary>
    public bool SourceOutputIsValidJson { get; set; }
}