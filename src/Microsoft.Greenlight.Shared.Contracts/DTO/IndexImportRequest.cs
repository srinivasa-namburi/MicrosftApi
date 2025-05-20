using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Request DTO for starting an index import job.
    /// </summary>
    public class IndexImportRequest
    {
        /// <summary>
        /// Gets or sets the table name to import into.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database schema.
        /// </summary>
        public string Schema { get; set; } = "km";
        
        /// <summary>
        /// Gets or sets the URL of the blob containing the import data.
        /// </summary>
        public string BlobUrl { get; set; } = string.Empty;
    }
}