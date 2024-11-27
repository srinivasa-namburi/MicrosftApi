using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    public class DynamicPluginInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string BlobContainerName { get; set; }
        public DynamicPluginVersionInfo LatestVersion { get; set; }
        public List<DynamicPluginVersionInfo> Versions { get; set; }
        public List<DynamicPluginDocumentProcessInfo> DocumentProcesses { get; set; }
    }
}
