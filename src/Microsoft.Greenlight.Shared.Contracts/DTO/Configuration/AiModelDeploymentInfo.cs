using Microsoft.Greenlight.Shared.Contracts.Components;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration
{
    /// <summary>
    /// Defines a deployment of an AI model with specific token settings and a deployment name.
    /// </summary>
    public class AiModelDeploymentInfo
    {

        /// <summary>
        /// ID of the deployment. This is the unique identifier for the deployment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The name of the deployment. This must match the deployment name on the Azure OpenAI instance
        /// </summary>
        public required string DeploymentName { get; set; }

        /// <summary>
        /// ID of the base AiModel that this deployment is based on.
        /// </summary>
        public Guid AiModelId { get; set; }
        
        /// <summary>
        /// AiModel that this deployment is based on. Not guaranteed to be populated (use AiModelId for that).
        /// </summary>
        public AiModelInfo? AiModel { get; set; }
        
        /// <summary>
        /// Token settings for various types of tasks. These are read at runtime and can be overridden here.
        /// </summary>
        public AiModelMaxTokenSettings TokenSettings { get; set; } = new AiModelMaxTokenSettings();

        /// <summary>
        /// Various reasoning settings for different types of AI model tasks.
        /// </summary>
        public AiModelReasoningSettings? ReasoningSettings { get; set; }
    }
}