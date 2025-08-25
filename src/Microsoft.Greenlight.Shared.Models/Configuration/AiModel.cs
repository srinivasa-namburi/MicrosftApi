using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.Configuration
{
    /// <summary>
    /// Model representing an AI model type (Such as gpt-3.5, gpt-4o, o3-mini, text-embedding-ada-002 etc).
    /// Also defines the default capabilities of the model.
    /// </summary>
    public class AiModel : EntityBase
    {
        /// <summary>
        /// Name of the model. Recommended to be the same as the model identifier.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Type of AI model (Chat or Embedding).
        /// </summary>
        public AiModelType ModelType { get; set; } = AiModelType.Chat;

        /// <summary>
        /// Various recommended max token limits for different types of content generation.
        /// These form the basis of deployment definitions, but are not enforced and can be overridden.
        /// Only relevant for Chat models.
        /// </summary>
        public AiModelMaxTokenSettings TokenSettings { get; set; } = new AiModelMaxTokenSettings();

        /// <summary>
        /// Whether or not this is a reasoning model type. Defaults to false.
        /// Only relevant for Chat models.
        /// </summary>
        public bool IsReasoningModel { get; set; } = false;

        /// <summary>
        /// Settings specific to embedding models, including dimensions and content length limits.
        /// Only relevant for Embedding models.
        /// </summary>
        public AiModelEmbeddingSettings? EmbeddingSettings { get; set; }
    }
}