namespace Microsoft.Greenlight.Shared.Models.Plugins;

public class DynamicPlugin : EntityBase
{
    public string Name { get; set; }
    public string BlobContainerName { get; set; }
    public List<DynamicPluginVersion> Versions { get; set; } = new List<DynamicPluginVersion>();
    public List<DynamicPluginDocumentProcess> DocumentProcesses { get; set; }

    public DynamicPluginVersion LatestVersion => Versions.Max();

    public string GetBlobName(DynamicPluginVersion version) 
        => $"{Name}/{version}/{Name}_{version}.zip";
}