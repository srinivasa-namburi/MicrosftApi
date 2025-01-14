namespace Microsoft.Greenlight.Extensions.Plugins
{
    /// <summary>
    /// Attribute to mark a class as a Greenlight plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GreenlightPluginAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the registration key for the plugin.
        /// </summary>
        public string RegistrationKey { get; set; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        public string? Version { get; }

        /// <summary>
        /// Gets the copyright holder of the plugin.
        /// </summary>
        public string? CopyrightHolder { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GreenlightPluginAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the plugin.</param>
        /// <param name="registrationKey">The registration key for the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <param name="copyrightHolder">The copyright holder of the plugin.</param>
        public GreenlightPluginAttribute(string name, string registrationKey, string? version = null,
            string? copyrightHolder = null)
        {
            Name = name;
            RegistrationKey = registrationKey;
            Version = version;
            CopyrightHolder = copyrightHolder;
        }
    }
}
