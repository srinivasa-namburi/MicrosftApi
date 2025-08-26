// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable outline package for a document process definition export/import.
    /// </summary>
    public class DocumentOutlinePackageDto
    {
        /// <summary>
        /// Original outline ID from the source system.
        /// </summary>
        public Guid OriginalDocumentOutlineId { get; set; }

        /// <summary>
        /// Optional full text form of the outline (if available).
        /// </summary>
        public string? FullText { get; set; }

        /// <summary>
        /// Root items of the outline hierarchy.
        /// </summary>
        public List<DocumentOutlineItemPackageDto> Items { get; set; } = new();
    }
}
