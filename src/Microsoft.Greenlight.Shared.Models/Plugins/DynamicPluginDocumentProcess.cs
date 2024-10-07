using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Plugins;

public class DynamicPluginDocumentProcess : EntityBase
{
    public Guid DynamicPluginId { get; set; }
    [JsonIgnore]
    public DynamicPlugin? DynamicPlugin { get; set; }

    public Guid DynamicDocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DynamicDocumentProcessDefinition { get; set; }

    public DynamicPluginVersion? Version { get; set; }
}