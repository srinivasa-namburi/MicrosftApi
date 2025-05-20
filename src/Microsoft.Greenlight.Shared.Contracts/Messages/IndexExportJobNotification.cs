using System;

namespace Microsoft.Greenlight.Shared.Contracts.Messages
{
    /// <summary>
    /// Notification sent to SignalR clients when an index export job completes or fails.
    /// </summary>
    public class IndexExportJobNotification
    {
        public Guid JobId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string? BlobUrl { get; set; }
        public string? Error { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public DateTimeOffset Started { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}
