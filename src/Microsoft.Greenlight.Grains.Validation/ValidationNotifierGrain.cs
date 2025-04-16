using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Validation
{
    [StatelessWorker]
    public class ValidationNotifierGrain : Grain, IValidationNotifierGrain
    {
        private readonly ILogger<ValidationNotifierGrain> _logger;

        public ValidationNotifierGrain(ILogger<ValidationNotifierGrain> logger)
        {
            _logger = logger;
        }

        public async Task NotifyValidationPipelineCompletedAsync(Guid validationExecutionId, Guid generatedDocumentId)
        {
            try
            {
                _logger.LogInformation("Publishing validation pipeline completion notification for {ExecutionId}", validationExecutionId);
                
                // Create notification message
                var notification = new ValidationExecutionForDocumentNotification(
                    correlationId: validationExecutionId, 
                    generatedDocumentId)
                {
                    NotificationType = ValidationExecutionStatusNotificationType.ValidationExecutionCompleted
                };

                // Send the notification via SignalR notifier grain
                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyValidationExecutionForDocumentAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing validation completion notification for {ExecutionId}", validationExecutionId);
            }
        }

        public async Task NotifyValidationPipelineFailedAsync(Guid validationExecutionId, Guid generatedDocumentId, string errorMessage)
        {
            try
            {
                _logger.LogInformation("Publishing validation pipeline failure notification for {ExecutionId}", validationExecutionId);
                
                // Create notification message
                var notification = new ValidationExecutionForDocumentNotification(
                    correlationId: validationExecutionId, 
                    generatedDocumentId)
                {
                    NotificationType = ValidationExecutionStatusNotificationType.ValidationExecutionFailed
                };

                // Send the notification via SignalR notifier grain
                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyValidationExecutionForDocumentAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing validation failure notification for {ExecutionId}", validationExecutionId);
            }
        }

        public async Task NotifyValidationExecutionForDocumentAsync(ValidationExecutionForDocumentNotification notification)
        {
            try
            {
                _logger.LogInformation("Forwarding validation execution notification for document {DocumentId}", notification.GeneratedDocumentId);

                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyValidationExecutionForDocumentAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding validation execution notification for document {DocumentId}", notification.GeneratedDocumentId);
            }
        }
    }
}
