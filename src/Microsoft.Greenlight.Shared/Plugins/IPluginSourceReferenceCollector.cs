using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.Plugins;

public interface IPluginSourceReferenceCollector
{
    void Add(Guid executionId, PluginSourceReferenceItem item);
    IList<PluginSourceReferenceItem> GetAll(Guid executionId);
    void Clear(Guid executionId);
}