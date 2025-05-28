namespace Microsoft.Greenlight.Shared.Contracts.Streams
{
    /// <summary>
    /// Namespaces for system streams for various system change notifications
    /// </summary>
    public static class SystemStreamNameSpaces
    {
        /// <summary>
        /// This stream receives messages about configuration updates and triggers a reload
        /// of the configuration subsystem.
        /// </summary>
        public static string ConfigurationUpdatedNamespace => "ConfigurationUpdated";

        /// <summary>
        /// This stream receives messages about plugin updates (stop/remove) and triggers plugin manager actions.
        /// </summary>
        public static string PluginUpdateNamespace => "PluginUpdate";
    }
}