using Microsoft.Greenlight.Shared.Contracts.Components;



namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration
{
    /// <summary>
    /// Contract representing an AI model type (Such as gpt-3.5, gpt-4o, o3-mini etc).
    /// Also defines the default capabilities of the model.
    /// </summary>
    public class AiModelInfo
    {
        /// <summary>
        /// ID of the model. This is the unique identifier for the model.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the model. Recommended to be the same as the model identifier.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Various recommended max token limits for different types of content generation.
        /// These form the basis of deployment definitions, but are not enforced and can be overridden.
        /// </summary>
        public AiModelMaxTokenSettings TokenSettings { get; set; } = new AiModelMaxTokenSettings();

        /// <summary>
        /// Whether this is a reasoning model type. Defaults to false.
        /// </summary>
        public bool IsReasoningModel { get; set; } = false;

    }
}