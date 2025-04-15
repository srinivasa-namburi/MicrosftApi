using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.State
{
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class DocumentIngestionState
    {
        public Guid Id { get; set; } = Guid.Empty;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        public IngestionOrchestrationState Status { get; set; } = IngestionOrchestrationState.NotStarted;
        public string DocumentLibraryShortName { get; set; } = string.Empty;
        public DocumentLibraryType DocumentLibraryType { get; set; }
        public string TargetContainerName { get; set; } = string.Empty;
        public int TotalFiles { get; set; } = 0;
        public int ProcessedFiles { get; set; } = 0;
        public int FailedFiles { get; set; } = 0;
        public List<string> Errors { get; set; } = new List<string>();
    }

    public enum IngestionOrchestrationState
    {
        NotStarted,
        CopyingFiles,
        ProcessingDocuments,
        Completed,
        Failed
    }
}