using Microsoft.Greenlight.Shared.Contracts.Components;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Configuration
{
    /// <summary>
    /// Defines a deployment of an AI model with specific token settings and a deployment name.
    /// </summary>
    public class AiModelDeployment : EntityBase
    {
        /// <inheritdoc />
        public AiModelDeployment()
        {

        }

        /// <summary>
        /// Creates a new deployment from an existing AI model with the same token settings.
        /// </summary>
        /// <param name="fromAiModel"></param>
        public AiModelDeployment(AiModel fromAiModel)
        {
            TokenSettings = fromAiModel.TokenSettings;
            DeploymentName = fromAiModel.Name;
            AiModelId = fromAiModel.Id;
            AiModel = fromAiModel;

            if (fromAiModel.IsReasoningModel)
            {
                ReasoningSettings = new AiModelReasoningSettings();
            }

            // Copy embedding settings if this is an embedding model
            if (fromAiModel.ModelType == Shared.Enums.AiModelType.Embedding && fromAiModel.EmbeddingSettings != null)
            {
                EmbeddingSettings = new AiModelEmbeddingSettings
                {
                    Dimensions = fromAiModel.EmbeddingSettings.Dimensions,
                    MaxContentLength = fromAiModel.EmbeddingSettings.MaxContentLength
                };
            }
        }

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
        [JsonIgnore]
        public AiModel? AiModel { get; set; }
        
        /// <summary>
        /// Token settings for various types of tasks. These are read at runtime and can be overridden here.
        /// Only relevant for Chat models.
        /// </summary>
        public AiModelMaxTokenSettings TokenSettings { get; set; } = new AiModelMaxTokenSettings();

        /// <summary>
        /// Various reasoning settings for different types of AI model tasks.
        /// Only relevant for Chat models with reasoning capabilities.
        /// </summary>
        public AiModelReasoningSettings? ReasoningSettings { get; set; }

        /// <summary>
        /// Settings specific to embedding models, including dimensions and content length limits.
        /// Only relevant for Embedding models. Can override the base model settings.
        /// </summary>
        public AiModelEmbeddingSettings? EmbeddingSettings { get; set; }
    }
}