// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable outline item for export/import with preserved parent/child relationships.
    /// </summary>
    public class DocumentOutlineItemPackageDto
    {
        /// <summary>
        /// Original outline item ID from the source system.
        /// </summary>
        public Guid OriginalId { get; set; }

        /// <summary>
        /// Section number if available (e.g., 1, 1.1, etc.).
        /// </summary>
        public string? SectionNumber { get; set; }

        /// <summary>
        /// Section title text.
        /// </summary>
        public string SectionTitle { get; set; } = string.Empty;

        /// <summary>
        /// Hierarchy level (0-based).
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Additional prompt instructions for this section, if any.
        /// </summary>
        public string? PromptInstructions { get; set; }

        /// <summary>
        /// If true, render only the section title with no generated content.
        /// </summary>
        public bool RenderTitleOnly { get; set; }

        /// <summary>
        /// Order of the item within its siblings.
        /// </summary>
        public int? OrderIndex { get; set; }

        /// <summary>
        /// Child items under this item.
        /// </summary>
        public List<DocumentOutlineItemPackageDto> Children { get; set; } = new();
    }
}
