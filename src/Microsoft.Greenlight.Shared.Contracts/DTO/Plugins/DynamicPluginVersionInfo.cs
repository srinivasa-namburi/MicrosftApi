namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Represents the version information for a dynamic plugin.
    /// </summary>
    public class DynamicPluginVersionInfo
    {
        /// <summary>
        /// Major version number.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Minor version number.
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Patch version number.
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Overrides ToString to format version information.
        /// </summary>
        /// <returns>A string that represents the version info.</returns>
        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}