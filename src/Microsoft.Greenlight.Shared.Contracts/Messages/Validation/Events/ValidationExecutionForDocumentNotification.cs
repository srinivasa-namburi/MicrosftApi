using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    /// <summary>
    /// Notification indicating that various stages of validation execution has reached a certain state.
    /// </summary>
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public record ValidationExecutionForDocumentNotification 
    {
        /// <summary>
        /// Constructor for the ValidationExecutionForDocumentNotification message
        /// </summary>
        /// <param name="correlationId">The Validation Pipeline Execution ID</param>
        /// <param name="generatedDocumentId">The ID of the Generated Document</param>
        public ValidationExecutionForDocumentNotification(Guid correlationId, Guid generatedDocumentId)
        {
            GeneratedDocumentId = generatedDocumentId;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// ID of the generated document for which validation execution has started.
        /// </summary>
        public Guid GeneratedDocumentId { get; set; }

        /// <summary>
        /// ValidationPipelineExecution ID that was created for this validation execution.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Type of Notification
        /// </summary>
        public required ValidationExecutionStatusNotificationType NotificationType { get; init; } = ValidationExecutionStatusNotificationType.ValidationExecutionStarted;

        /// <summary>
        /// If Notification Type is a ValidationStepCompleted, this will hold the content node changes - if any.
        /// </summary>
        public ValidationExecutionStepContentNodeResultInfo? ContentNodeChangeResult { get; set; }

        /// <summary>
        /// Whether this notification indicates that changes were requested for the content node in a validation step.
        /// </summary>
        [JsonIgnore]
        public bool HasRecommendedChanges
        {
            get
            {
                if (NotificationType != ValidationExecutionStatusNotificationType.ValidationStepContentChangeRequested ||
                    ContentNodeChangeResult == null)
                {
                    return false;
                }

                return ContentNodeChangeResult.OriginalContentNodeId != ContentNodeChangeResult.ResultantContentNodeId;
            }
        }
    }
}