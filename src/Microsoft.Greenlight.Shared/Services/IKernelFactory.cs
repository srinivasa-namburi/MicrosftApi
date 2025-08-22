using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.AI;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Factory to retrieve a new or existing kernel instance for a document process.
    /// 
    /// </summary>
    public interface IKernelFactory
    {
        /// <summary>
        /// Retrieve a Semantic Kernel instance with plugins for a document process.
        /// </summary>
        /// <param name="documentProcess">DocumentProcessInfo object representing a document process</param>
        /// <returns></returns>
        public Task<Kernel> GetKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess);
        /// <summary>
        /// Retrieve a Semantic Kernel instance with plugins for a document process.
        /// </summary>
        /// <param name="documentProcessName">ShortName for a document process</param>
        /// <returns></returns>
        public Task<Kernel> GetKernelForDocumentProcessAsync(string documentProcessName);

        /// <summary>
        /// Retrieve a Semantic Kernel instance used for validation for a document process.
        /// </summary>
        /// <param name="documentProcess">DocumentProcessInfo object representing a document process</param>
        public Task<Kernel> GetValidationKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess);

        /// <summary>
        /// Retrieve a Semantic Kernel instance used for validation for a document process.
        /// </summary>
        /// <param name="documentProcessName">ShortName for a document process</param>
        public Task<Kernel> GetValidationKernelForDocumentProcessAsync(string documentProcessName);

        /// <summary>
        /// Retrieve a Semantic Kernel with no plugins with a specific model identifier.
        /// </summary>
        /// <param name="modelIdentifier">Identifier for an Azure OpenAI model deployment to use</param>
        /// <returns></returns>
        public Task<Kernel> GetGenericKernelAsync(string modelIdentifier);

        /// <summary>
        /// Returns the prompt execution settings for a document process.
        /// </summary>
        /// <param name="documentProcess"></param>
        /// <param name="aiTaskType"></param>
        /// <returns></returns>
        public Task<AzureOpenAIPromptExecutionSettings> GetPromptExecutionSettingsForDocumentProcessAsync(
            DocumentProcessInfo documentProcess, AiTaskType aiTaskType);

        /// <summary>
        /// Returns the prompt execution settings for a document process.
        /// </summary>
        /// <param name="documentProcessName"></param>
        /// <param name="aiTaskType"></param>
        Task<AzureOpenAIPromptExecutionSettings> GetPromptExecutionSettingsForDocumentProcessAsync(
            string documentProcessName, AiTaskType aiTaskType);

        /// <summary>
        /// Returns unified ChatOptions for a document process &amp; task type (Microsoft.Extensions.AI abstraction).
        /// </summary>
        /// <param name="documentProcessName">Short name of the document process.</param>
        /// <param name="aiTaskType">Task type guiding configuration.</param>
        Task<ChatOptions> GetChatOptionsForDocumentProcessAsync(string documentProcessName, AiTaskType aiTaskType);

        /// <summary>
        /// Gets a default generic kernel instance with the gpt-4o deployment for the solution
        /// </summary>
        /// <returns></returns>
        Task<Kernel> GetDefaultGenericKernelAsync();
    }
}