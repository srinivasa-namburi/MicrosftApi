namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents a request to update or add configuration values.
    /// </summary>
    public class ConfigurationUpdateRequest
    {
        /// <summary>
        /// The key-value pairs of configuration items to be updated or added.
        /// </summary>
        public Dictionary<string, string> ConfigurationItems { get; set; } = new();
    }
}