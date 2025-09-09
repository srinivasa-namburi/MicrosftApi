using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Microsoft.Greenlight.Shared.Hubs;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.ApiSpecific
{
    /// <summary>
    /// Grain that handles sending SignalR notifications to clients
    /// </summary>
    [StatelessWorker]
    [Reentrant]
    public class SignalRNotifierGrain : Grain, ISignalRNotifierGrain
    {
        private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
        private readonly ILogger<SignalRNotifierGrain> _logger;

        public SignalRNotifierGrain(
            IHubContext<NotificationHub, INotificationHubClient> hubContext,
            ILogger<SignalRNotifierGrain> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        #region Document Domain Notifications

        /// <inheritdoc />
        public async Task NotifyDocumentOutlineGeneratedAsync(DocumentOutlineGeneratedNotification notification)
        {
            try
            {
                string groupId = notification.CorrelationId.ToString();
                await _hubContext.Clients.Group(groupId).ReceiveDocumentOutlineNotification(groupId);
                _logger.LogInformation("Sent document outline generated notification for document {DocumentId}",
                    notification.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document outline generated notification for document {DocumentId}",
                    notification.CorrelationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyContentNodeStateChangedAsync(ContentNodeGenerationStateChanged notification)
        {
            try
            {
                string groupId = notification.CorrelationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentNodeGenerationStateChangedNotification(notification);

                _logger.LogInformation("Sent content node state changed notification for node {NodeId} in document {DocumentId}",
                    notification.ContentNodeId, notification.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending content node state changed notification for node {NodeId} in document {DocumentId}",
                    notification.ContentNodeId, notification.CorrelationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyDocumentOutlineGenerationFailedAsync(DocumentOutlineGenerationFailed notification)
        {
            // We don't have a notification for this yet, but we can add it later if needed.
            //try
            //{
            //    string groupId = notification.CorrelationId.ToString();
            //    await _hubContext.Clients.Group(groupId).ReceiveDocumentOutlineGenerationFailedNotification(notification);
            //    _logger.LogInformation("Sent document outline generation failed notification for document {DocumentId}", 
            //        notification.CorrelationId);
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Error sending document outline generation failed notification for document {DocumentId}", 
            //        notification.CorrelationId);
            //}
            return;
        }

        #endregion

        #region Chat Domain Notifications

        /// <inheritdoc />
        public async Task NotifyChatMessageStatusAsync(ChatMessageStatusNotification notification)
        {
            try
            {
                string groupId = notification.ChatMessageId.ToString();

                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveChatMessageStatusNotification(notification);

                _logger.LogInformation("Sent chat message status notification for message {MessageId}: {Message}",
                    notification.ChatMessageId, notification.StatusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message status notification for message {MessageId}",
                    notification.ChatMessageId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyChatMessageResponseReceivedAsync(ChatMessageResponseReceived notification)
        {
            try
            {
                string groupId = notification.ChatMessageDto.ConversationId.ToString();

                await _hubContext.Clients
                    .Group(groupId).
                    ReceiveChatMessageResponseReceivedNotification(notification);

                _logger.LogInformation("Sent chat message response notification for conversation {ConversationId}",
                    notification.ChatMessageDto.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message response notification for conversation {ConversationId}",
                    notification.ChatMessageDto.ConversationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyConversationReferencesUpdatedAsync(ConversationReferencesUpdatedNotification notification)
        {
            try
            {
                string groupId = notification.ConversationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveConversationReferencesUpdatedNotification(notification);

                _logger.LogInformation("Sent conversation references update notification for conversation {ConversationId}",
                    notification.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending conversation references update notification for conversation {ConversationId}",
                    notification.ConversationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyContentChunkUpdateAsync(ContentChunkUpdate notification)
        {
            try
            {
                string groupId = notification.ConversationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentChunkUpdateNotification(notification);

                _logger.LogInformation("Sent content chunk update notification for conversation {ConversationId}, chunk count {ChunkCount}",
                    notification.ConversationId, notification.Chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending content chunk update notification for conversation {ConversationId}",
                    notification.ConversationId);
            }
        }

        #endregion

        #region Validation Domain Notifications

        // <inheritdoc />
        public async Task NotifyValidationPipelineCompletedAsync(Guid validationExecutionId, Guid generatedDocumentId)
        {
            try
            {
                string groupId = validationExecutionId.ToString();
                //await _hubContext.Clients.Group(groupId).SendAsync("ValidationPipelineCompleted", new
                //{
                //    ValidationExecutionId = validationExecutionId,
                //    GeneratedDocumentId = generatedDocumentId
                //});

                _logger.LogInformation("Sent validation pipeline completed notification for execution {ExecutionId}", validationExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending validation pipeline completed notification for execution {ExecutionId}", validationExecutionId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyValidationPipelineFailedAsync(Guid validationExecutionId, Guid generatedDocumentId, string errorMessage)
        {
            try
            {
                string groupId = validationExecutionId.ToString();
                //await _hubContext.Clients.Group(groupId).SendAsync("ValidationPipelineFailed", new
                //{
                //    ValidationExecutionId = validationExecutionId,
                //    GeneratedDocumentId = generatedDocumentId,
                //    ErrorMessage = errorMessage
                //});

                _logger.LogInformation("Sent validation pipeline failed notification for execution {ExecutionId}", validationExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending validation pipeline failed notification for execution {ExecutionId}", validationExecutionId);
            }
        }

        public async Task NotifyValidationExecutionForDocumentAsync(ValidationExecutionForDocumentNotification notification)
        {
            try
            {
                string groupId = notification.GeneratedDocumentId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveValidationExecutionForDocumentNotification(notification);

                _logger.LogInformation("Sent validation execution notification for document {DocumentId}", notification.GeneratedDocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending validation execution notification for document {DocumentId}", notification.GeneratedDocumentId);
            }
        }

        #endregion

        #region Review Domain Notifications

        public async Task NotifyBackendProcessingMessageAsync(BackendProcessingMessageGenerated notification)
        {
            try
            {
                string groupId = notification.CorrelationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveBackendProcessingMessageGeneratedNotification(notification);

                _logger.LogInformation("Sent backend processing message notification for review instance {InstanceId}: {Message}",
                    notification.CorrelationId, notification.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending backend processing message notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
        }

        public async Task NotifyReviewQuestionAnsweredAsync(ReviewQuestionAnsweredNotification notification)
        {
            try
            {
                string groupId = notification.CorrelationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveReviewQuestionAnsweredNotification(notification);

                _logger.LogInformation("Sent review question answered notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending review question answered notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
        }

        public async Task NotifyReviewCompletedAsync(ReviewCompletedNotification notification)
        {
            try
            {
                string groupId = notification.CorrelationId.ToString();
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveReviewCompletedNotification(notification);

                _logger.LogInformation("Sent review completed notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending review completed notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
        }

        #endregion

        #region Index Export/Import Notifications

        /// <inheritdoc/>
        public async Task NotifyExportJobCompletedAsync(string userGroup, IndexExportJobNotification notification)
        {
            try
            {
                await _hubContext.Clients
                    .Group(userGroup)
                    .ReceiveExportJobCompletedNotification(notification);

                _logger.LogInformation("Sent export job completed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending export job completed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
        }

        /// <inheritdoc/>
        public async Task NotifyExportJobFailedAsync(string userGroup, IndexExportJobNotification notification)
        {
            try
            {
                await _hubContext.Clients
                    .Group(userGroup)
                    .ReceiveExportJobFailedNotification(notification);

                _logger.LogInformation("Sent export job failed notification for table {TableName}, job {JobId}: {Error}",
                    notification.TableName, notification.JobId, notification.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending export job failed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
        }

        /// <inheritdoc/>
        public async Task NotifyImportJobCompletedAsync(string userGroup, IndexImportJobNotification notification)
        {
            try
            {
                await _hubContext.Clients
                    .Group(userGroup)
                    .ReceiveImportJobCompletedNotification(notification);

                _logger.LogInformation("Sent import job completed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending import job completed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
        }

        /// <inheritdoc/>
        public async Task NotifyImportJobFailedAsync(string userGroup, IndexImportJobNotification notification)
        {
            try
            {
                await _hubContext.Clients
                    .Group(userGroup)
                    .ReceiveImportJobFailedNotification(notification);

                _logger.LogInformation("Sent import job failed notification for table {TableName}, job {JobId}: {Error}",
                    notification.TableName, notification.JobId, notification.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending import job failed notification for table {TableName}, job {JobId}",
                    notification.TableName, notification.JobId);
            }
        }

        #endregion

        #region Document Reindexing Notifications

        /// <inheritdoc />
        public async Task NotifyDocumentReindexStartedAsync(DocumentReindexStartedNotification notification)
        {
            try
            {
                // Contribute to system status
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveDocumentReindexStartedNotification(notification);

                _logger.LogInformation("Sent document reindex started notification for {LibraryOrProcess}, orchestration {OrchestrationId}",
                    notification.DocumentLibraryOrProcessName, notification.OrchestrationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document reindex started notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyDocumentReindexProgressAsync(DocumentReindexProgressNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveDocumentReindexProgressNotification(notification);

                _logger.LogInformation("Sent document reindex progress notification for {LibraryOrProcess}: {Processed}/{Total}",
                    notification.DocumentLibraryOrProcessName, notification.ProcessedDocuments, notification.TotalDocuments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document reindex progress notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyDocumentReindexCompletedAsync(DocumentReindexCompletedNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveDocumentReindexCompletedNotification(notification);

                _logger.LogInformation("Sent document reindex completed notification for {LibraryOrProcess}, processed {Processed}/{Total}",
                    notification.DocumentLibraryOrProcessName, notification.ProcessedDocuments, notification.TotalDocuments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document reindex completed notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        /// <inheritdoc />
        public async Task NotifyDocumentReindexFailedAsync(DocumentReindexFailedNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveDocumentReindexFailedNotification(notification);

                _logger.LogInformation("Sent document reindex failed notification for {LibraryOrProcess}: {Error}",
                    notification.DocumentLibraryOrProcessName, notification.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document reindex failed notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        #endregion

        #region System Status

        /// <inheritdoc />
        public async Task<SystemStatusSnapshot> GetSystemStatusAsync()
        {
            try
            {
                var aggregatorGrain = GrainFactory.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
                return await aggregatorGrain.GetSystemStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system status");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SubsystemStatus?> GetSubsystemStatusAsync(string source)
        {
            try
            {
                var aggregatorGrain = GrainFactory.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
                return await aggregatorGrain.GetSubsystemStatusAsync(source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subsystem status for {Source}", source);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task NotifySystemStatusUpdateAsync(SystemStatusSnapshot statusSnapshot)
        {
            try
            {
                await _hubContext.Clients
                    .Group("system-status")
                    .ReceiveSystemStatusUpdateNotification(statusSnapshot);

                _logger.LogDebug("Sent system status update notification to system-status group: {OverallStatus}, {SubsystemCount} subsystems",
                    statusSnapshot.OverallStatus, statusSnapshot.Subsystems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system status update notification");
            }
        }

        #endregion

        #region Content Reference Reindexing Notifications

        public async Task NotifyContentReferenceReindexStartedAsync(ContentReferenceReindexStartedNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentReferenceReindexStartedNotification(notification);

                _logger.LogInformation("Sent CR reindex started notification for {Type}, orchestration {OrchestrationId}",
                    notification.ReferenceType, notification.OrchestrationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CR reindex started notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        public async Task NotifyContentReferenceReindexProgressAsync(ContentReferenceReindexProgressNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentReferenceReindexProgressNotification(notification);

                _logger.LogInformation("Sent CR reindex progress notification for {Type}: {Processed}/{Total}",
                    notification.ReferenceType, notification.Processed, notification.Total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CR reindex progress notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        public async Task NotifyContentReferenceReindexCompletedAsync(ContentReferenceReindexCompletedNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentReferenceReindexCompletedNotification(notification);

                _logger.LogInformation("Sent CR reindex completed notification for {Type}, processed {Processed}/{Total}",
                    notification.ReferenceType, notification.Processed, notification.Total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CR reindex completed notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        public async Task NotifyContentReferenceReindexFailedAsync(ContentReferenceReindexFailedNotification notification)
        {
            try
            {
                await TryProcessStatusContributionAsync(notification);
                string groupId = notification.OrchestrationId;
                await _hubContext.Clients
                    .Group(groupId)
                    .ReceiveContentReferenceReindexFailedNotification(notification);

                _logger.LogInformation("Sent CR reindex failed notification for {Type}: {Error}",
                    notification.ReferenceType, notification.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CR reindex failed notification for orchestration {OrchestrationId}",
                    notification.OrchestrationId);
            }
        }

        #endregion

        private async Task TryProcessStatusContributionAsync(object notification)
        {
            try
            {
                if (notification is ISystemStatusNotification statusNotification)
                {
                    var aggregatorGrain = GrainFactory.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
                    await aggregatorGrain.ProcessStatusContributionAsync(statusNotification.GetStatusContribution());
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to process system status contribution from notification");
            }
        }
    }
}
