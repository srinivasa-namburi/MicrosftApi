namespace Microsoft.Greenlight.Grains.Shared.Contracts.Models
{
    /// <summary>
    /// Status of an index import job.
    /// </summary>
    public class IndexImportJobStatus
    {
        /// <summary>
        /// Gets or sets the job ID.
        /// </summary>
        public Guid JobId { get; set; }
        
        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the blob URL where the import data is stored.
        /// </summary>
        public string? BlobUrl { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the job failed.
        /// </summary>
        public string? Error { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the job is completed.
        /// </summary>
        public bool IsCompleted { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether the job has failed.
        /// </summary>
        public bool IsFailed => !string.IsNullOrEmpty(Error);
        
        /// <summary>
        /// Gets or sets when the job was started.
        /// </summary>
        public DateTimeOffset Started { get; set; }
        
        /// <summary>
        /// Gets or sets when the job was completed.
        /// </summary>
        public DateTimeOffset? Completed { get; set; }
    }
}