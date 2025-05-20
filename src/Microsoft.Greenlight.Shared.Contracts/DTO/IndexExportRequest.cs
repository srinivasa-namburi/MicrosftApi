using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Request DTO for starting an index export job.
    /// </summary>
    public class IndexExportRequest
    {
        /// <summary>
        /// Gets or sets the table name to export.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database schema.
        /// </summary>
        public string Schema { get; set; } = "km";
    }
}