// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable representation of a process metadata field for export/import.
    /// </summary>
    public class DocumentProcessMetaDataFieldPackageDto
    {
        /// <summary>
        /// Original metadata field ID from the source system.
        /// </summary>
        public Guid OriginalId { get; set; }

        /// <summary>
        /// Field name (key in metadata model).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional description/tooltip.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Field type as string (maps to DynamicDocumentProcessMetaDataFieldType).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Whether the field is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Order within the form.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Default value for the field.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Optional JSON schema or constraints (reserved for future use).
        /// </summary>
        public string? JsonSchema { get; set; }

        /// <summary>
        /// Whether a closed set of possible values is provided.
        /// </summary>
        public bool HasPossibleValues { get; set; }

        /// <summary>
        /// Possible values for selection fields.
        /// </summary>
        public List<string> PossibleValues { get; set; } = new();

        /// <summary>
        /// Default possible value (must exist in PossibleValues when provided).
        /// </summary>
        public string? DefaultPossibleValue { get; set; }
    }
}
