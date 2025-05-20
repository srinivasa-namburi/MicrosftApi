using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Status of an index import job.
    /// </summary>
    public class IndexImportJobStatus
    {
        public Guid JobId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string? BlobUrl { get; set; }
        public string? Error { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed => !string.IsNullOrEmpty(Error);
        public DateTimeOffset Started { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}