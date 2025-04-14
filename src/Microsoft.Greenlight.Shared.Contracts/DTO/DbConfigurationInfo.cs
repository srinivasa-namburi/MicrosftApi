namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents the configuration values stored in the database.
    /// </summary>
    public class DbConfigurationInfo
    {
        /// <summary>
        /// Default ID - should be the only one used.
        /// </summary>
        public static Guid DefaultId => Guid.Parse("52d7cb18-1543-4156-b535-8a7defbf9066");
        /// <summary>
        /// The primary key for the configuration record.
        /// </summary>
        public Guid Id { get; set; }

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