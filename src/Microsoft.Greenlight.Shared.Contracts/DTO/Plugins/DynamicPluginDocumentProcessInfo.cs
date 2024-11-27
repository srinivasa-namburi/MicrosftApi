using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    public class DynamicPluginDocumentProcessInfo
    {
        public Guid Id { get; set; }
        public Guid DynamicPluginId { get; set; }
        public Guid DynamicDocumentProcessDefinitionId { get; set; }
        public DynamicPluginVersionInfo Version { get; set; }
    }
}
