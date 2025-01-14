using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Plugins;

/// <summary>
/// Represents the association between a dynamic plugin and a dynamic document process definition.
/// </summary>
public class DynamicPluginDocumentProcess : EntityBase
{
    /// <summary>
    /// Unique identifier of the dynamic plugin.
    /// </summary>
    public Guid DynamicPluginId { get; set; }

    /// <summary>
    /// Dynamic plugin associated with this document process.
    /// </summary>
    [JsonIgnore]
    public DynamicPlugin? DynamicPlugin { get; set; }

    /// <summary>
    /// Unique identifier of the dynamic document process definition.
    /// </summary>
    public Guid DynamicDocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Dynamic document process definition associated with this document process.
    /// </summary>
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DynamicDocumentProcessDefinition { get; set; }

    /// <summary>
    /// Version of the dynamic plugin.
    /// </summary>
    public DynamicPluginVersion? Version { get; set; }
}
