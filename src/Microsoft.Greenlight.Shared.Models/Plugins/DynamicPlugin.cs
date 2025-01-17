namespace Microsoft.Greenlight.Shared.Models.Plugins;

/// <summary>
/// Represents a dynamic plugin with various properties and methods.
/// </summary>
public class DynamicPlugin : EntityBase
{
    /// <summary>
    /// Name of the plugin.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Name of the blob container where the plugin is stored.
    /// </summary>
    public required string BlobContainerName { get; set; }

    /// <summary>
    /// List of versions for the plugin.
    /// </summary>
    public List<DynamicPluginVersion> Versions { get; set; } = [];

    /// <summary>
    /// List of document processes associated with the plugin.
    /// </summary>
    public List<DynamicPluginDocumentProcess> DocumentProcesses { get; set; } = [];

    /// <summary>
    /// Gets the latest version of the plugin.
    /// </summary>
    public DynamicPluginVersion? LatestVersion => Versions.Max();

    /// <summary>
    /// Gets the blob name for the specified version of the plugin.
    /// </summary>
    /// <param name="version">The version of the plugin.</param>
    /// <returns>The blob name for the specified version.</returns>
    public string GetBlobName(DynamicPluginVersion version)
        => $"{Name}/{version}/{Name}_{version}.zip";
}
