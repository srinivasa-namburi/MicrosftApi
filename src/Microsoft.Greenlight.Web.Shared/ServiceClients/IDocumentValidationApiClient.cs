using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients
    {
        /// <summary>
        /// Client for accessing document validation API endpoints
        /// </summary>
        public interface IDocumentValidationApiClient : IServiceClient
        {
            /// <summary>
            /// Start validation for a document
            /// </summary>
            /// <param name="documentId">The ID of the document to validate</param>
            /// <returns>True if validation was started successfully</returns>
            Task<bool> StartDocumentValidationAsync(string documentId);

            /// <summary>
            /// Get validation status for a document
            /// </summary>
            /// <param name="documentId">The ID of the document to check</param>
            /// <returns>Validation status information</returns>
            Task<ValidationStatusInfo> GetDocumentValidationStatusAsync(string documentId);

            /// <summary>
            /// Get the latest completed validation execution with any recommended changes for a document
            /// </summary>
            /// <param name="documentId">The ID of the document</param>
            /// <returns>Validation results with recommended changes if any</returns>
            Task<ValidationResultsInfo?> GetLatestValidationResultsAsync(string documentId);
        
            /// <summary>
            /// Update the application status of a validation execution
            /// </summary>
            /// <param name="validationExecutionId">The validation execution ID</param>
            /// <param name="status">The new application status</param>
            /// <returns>True if update was successful</returns>
            Task<bool> UpdateValidationApplicationStatusAsync(string validationExecutionId, ValidationPipelineExecutionApplicationStatus status);

            /// <summary>
            /// Updates the application status of a specific validation content change.
            /// </summary>
            /// <param name="contentChangeId">The ID of the validation content change.</param>
            /// <param name="status">The new application status.</param>
            /// <returns>True if the update was successful, otherwise false.</returns>
            Task<bool> UpdateValidationContentChangeStatusAsync(
                Guid contentChangeId,
                ValidationContentNodeApplicationStatus status);

            Task<DocumentProcessValidationPipelineInfo?> GetValidationPipelineConfigurationByProcessNameAsync(string processName);
        }
    }