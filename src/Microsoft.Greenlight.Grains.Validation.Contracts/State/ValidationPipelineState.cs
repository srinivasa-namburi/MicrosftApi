namespace Microsoft.Greenlight.Grains.Validation.Contracts.State
{
    [Serializable]
    public class ValidationPipelineState
    {
        public Guid Id { get; set; }
        public Guid GeneratedDocumentId { get; set; }
        public ValidationPipelineStatus Status { get; set; } = ValidationPipelineStatus.NotStarted;
        public List<ValidationPipelineStepInfo> OrderedSteps { get; set; } = new();
        public int CurrentStepIndex { get; set; } = -1;
        public string FailureReason { get; set; }
        public string FailureDetails { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}