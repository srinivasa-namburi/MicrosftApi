// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.State
{
    /// <summary>
    /// State for document ingestion orchestration. (No changes needed for DTO refactor, but doc added for clarity.)
    /// </summary>
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class DocumentIngestionState
    {
        public String Id { get; set; } = "";
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
        Running,
        Completed,
        Failed
    }
}