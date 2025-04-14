using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Orleans;

namespace Microsoft.Greenlight.Grains.Validation.Contracts
{
    public interface IValidationNotifierGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Notify clients that a validation pipeline has completed
        /// </summary>
        /// <param name="validationExecutionId">The validation execution ID</param>
        /// <param name="generatedDocumentId">The generated document ID</param>
        Task NotifyValidationPipelineCompletedAsync(Guid validationExecutionId, Guid generatedDocumentId);

        /// <summary>
        /// Notify clients that a validation pipeline has failed
        /// </summary>
        /// <param name="validationExecutionId">The validation execution ID</param>
        /// <param name="generatedDocumentId">The generated document ID</param>
        /// <param name="errorMessage">The error message</param>
        Task NotifyValidationPipelineFailedAsync(Guid validationExecutionId, Guid generatedDocumentId, string errorMessage);

        Task NotifyValidationExecutionForDocumentAsync(ValidationExecutionForDocumentNotification notification);
    }
}