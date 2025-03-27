namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents the configuration values stored in the database.
    /// </summary>
    public class DbConfigurationInfo
    {
        /// <summary>
        /// The primary key for the configuration record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The configuration values stored as a JSON string.
        /// </summary>
        public string ConfigurationValues { get; set; } = "{}";

        /// <summary>
        /// The timestamp when the configuration was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// The user who last updated the configuration.
        /// </summary>
        public string LastUpdatedBy { get; set; } = string.Empty;
    }
}