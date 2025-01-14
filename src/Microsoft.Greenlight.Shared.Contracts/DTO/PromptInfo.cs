namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents information about a prompt.
    /// </summary>
    public record PromptInfo
    {
        /// <summary>
        /// Unique identifier of the prompt.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Unique identifier of the prompt definition.
        /// </summary>
        public Guid DefinitionId { get; set; }

        /// <summary>
        /// Short code of the prompt.
        /// </summary>
        public required string ShortCode { get; set; }

        /// <summary>
        /// Description of the prompt.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Text of the prompt.
        /// </summary>
        public required string Text { get; set; }

        /// <summary>
        /// Unique identifier of the document process.
        /// </summary>
        public Guid? DocumentProcessId { get; set; }

        /// <summary>
        /// Name of the document process.
        /// </summary>
        public string? DocumentProcessName { get; set; }
    }
}
