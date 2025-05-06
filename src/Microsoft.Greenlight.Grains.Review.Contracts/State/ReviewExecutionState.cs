using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts.State
{
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class ReviewExecutionState
    {
        public Guid Id { get; set; }
        public Guid ReviewInstanceId { get; set; }
        public Guid? ExportedDocumentLinkId { get; set; }
        public ReviewExecutionStatus Status { get; set; } = ReviewExecutionStatus.NotStarted;
        public int TotalNumberOfQuestions { get; set; }
        public int NumberOfQuestionsAnswered { get; set; }
        public int NumberOfQuestionsAnalyzed { get; set; }
        public string? FailureReason { get; set; }
        public string? FailureDetails { get; set; }
        public string? ContentType { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public enum ReviewExecutionStatus
    {
        NotStarted,
        Started,
        Ingesting,
        DistributingQuestions,
        AnsweringQuestions,
        Completed,
        Failed
    }
}
