namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Represents the document process information for a dynamic plugin.
    /// </summary>
    public class DynamicPluginDocumentProcessInfo
    {
        /// <summary>
        /// Unique identifier for the document process info.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Unique identifier for the dynamic plugin.
        /// </summary>
        public Guid DynamicPluginId { get; set; }

        /// <summary>
        /// Unique identifier for the dynamic document process definition.
        /// </summary>
        public Guid DynamicDocumentProcessDefinitionId { get; set; }

        /// <summary>
        /// Version information for the dynamic plugin.
        /// </summary>
        public DynamicPluginVersionInfo Version { get; set; }
    }
}
