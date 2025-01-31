using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// Interface for collecting plugin source references.
/// </summary>
public interface IPluginSourceReferenceCollector
{
    /// <summary>
    /// Adds a plugin source reference item for the specified execution ID.
    /// </summary>
    /// <param name="executionId">The execution ID.</param>
    /// <param name="item">The plugin source reference item.</param>
    void Add(Guid executionId, PluginSourceReferenceItem item);

    /// <summary>
    /// Gets all plugin source reference items for the specified execution ID.
    /// </summary>
    /// <param name="executionId">The execution ID.</param>
    /// <returns>A list of plugin source reference items.</returns>
    IList<PluginSourceReferenceItem> GetAll(Guid executionId);

    /// <summary>
    /// Clears all plugin source reference items for the specified execution ID.
    /// </summary>
    /// <param name="executionId">The execution ID.</param>
    void Clear(Guid executionId);
}
