// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable prompt implementation representation for export/import.
    /// </summary>
    public class PromptImplementationPackageDto
    {
        /// <summary>
        /// Original implementation ID from source system.
        /// </summary>
        public Guid OriginalPromptImplementationId { get; set; }

        /// <summary>
        /// Original prompt definition ID from source system.
        /// </summary>
        public Guid OriginalPromptDefinitionId { get; set; }

        /// <summary>
        /// Prompt short code (stable reference across systems).
        /// </summary>
        public string ShortCode { get; set; } = string.Empty;

        /// <summary>
        /// Optional description from the PromptDefinition.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Text content of the prompt implementation.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}
