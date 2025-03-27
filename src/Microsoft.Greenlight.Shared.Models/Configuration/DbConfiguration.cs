// Microsoft.Greenlight.Shared.Models/Configuration/DbConfiguration.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Models.Configuration
{
    /// <summary>
    /// Represents configuration values stored in the database.
    /// </summary>
    public class DbConfiguration
    {
        /// <summary>
        /// The primary key for the configuration record.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The configuration values stored as a JSON string.
        /// </summary>
        public string ConfigurationValues { get; set; } = "{}";

        /// <summary>
        /// The timestamp when the configuration was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The user who last updated the configuration.
        /// </summary>
        public string LastUpdatedBy { get; set; } = string.Empty;
    }
}
