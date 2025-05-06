using Microsoft.Greenlight.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;

namespace Microsoft.Greenlight.Shared.Models.Plugins
{
    /// <summary>
    /// Represents a version of an MCP plugin.
    /// </summary>
    public class McpPluginVersion : EntityBase, IComparable<McpPluginVersion>
    {
        /// <summary>
        /// Gets or sets the major version number.
        /// </summary>
        [Required]
        public int Major { get; set; }
        
        /// <summary>
        /// Gets or sets the minor version number.
        /// </summary>
        [Required]
        public int Minor { get; set; }
        
        /// <summary>
        /// Gets or sets the patch version number.
        /// </summary>
        [Required]
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the MCP plugin identifier.
        /// </summary>
        public Guid McpPluginId { get; set; }
        
        /// <summary>
        /// Gets or sets the MCP plugin.
        /// </summary>
        public McpPlugin? McpPlugin { get; set; }

        /// <summary>
        /// Gets or sets the command to run for this version (overrides manifest).
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the arguments for the command (overrides manifest).
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the environment variables for this plugin version.
        /// These will be applied to the process when the plugin runs.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the URL for SSE/HTTP plugins. Null for non-SSE plugins.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the authentication type for the plugin. Null for non-SSE plugins.
        /// </summary>
        public McpPluginAuthenticationType? AuthenticationType { get; set; }

        /// <summary>
        /// Compares this instance with another McpPluginVersion.
        /// </summary>
        /// <param name="other">The other McpPluginVersion to compare with.</param>
        /// <returns>A value indicating the relative ordering.</returns>
        public int CompareTo(McpPluginVersion? other)
        {
            if (other == null)
            {
                return 1;
            }
            
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }
            
            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }
            
            return Patch.CompareTo(other.Patch);
        }

        /// <summary>
        /// Returns a string representation of the version.
        /// </summary>
        /// <returns>A string in the format "major.minor.patch".</returns>
        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }

        /// <summary>
        /// Creates a version object from a version string.
        /// </summary>
        /// <param name="versionString">The version string in the format "major.minor.patch".</param>
        /// <returns>A new McpPluginVersion object.</returns>
        public static McpPluginVersion Parse(string versionString)
        {
            var parts = versionString.Split('.');
            if (parts.Length != 3)
            {
                throw new FormatException($"Invalid version string format: {versionString}. Expected format is 'major.minor.patch'.");
            }
            
            if (!int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                throw new FormatException($"Invalid version string format: {versionString}. Version components must be integers.");
            }
            
            return new McpPluginVersion
            {
                Major = major,
                Minor = minor,
                Patch = patch
            };
        }

        /// <summary>
        /// Attempts to parse a version string to create a McpPluginVersion.
        /// </summary>
        /// <param name="versionString">The version string to parse.</param>
        /// <param name="version">When this method returns, contains the McpPluginVersion value equivalent to the version
        /// contained in versionString, if the conversion succeeded, or null if the conversion failed.</param>
        /// <returns>true if versionString was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string versionString, out McpPluginVersion? version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(versionString))
            {
                return false;
            }

            var parts = versionString.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                return false;
            }

            version = new McpPluginVersion
            {
                Major = major,
                Minor = minor,
                Patch = patch
            };

            return true;
        }
    }
}