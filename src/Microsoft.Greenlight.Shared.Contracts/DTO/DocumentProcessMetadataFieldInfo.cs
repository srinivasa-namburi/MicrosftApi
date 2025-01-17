using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents metadata field information for a document process.
    /// </summary>
    public class DocumentProcessMetadataFieldInfo
    {
        /// <summary>
        /// ID of the Metadata Field. Unique identifier.
        /// </summary>
        public Guid Id { get; set; } = Guid.Empty;

        /// <summary>
        /// Unique identifier of the Dynamic Document Process Definition this field belongs to.
        /// </summary>
        public Guid DynamicDocumentProcessDefinitionId { get; set; }

        /// <summary>
        /// Name of the field. Used as the key in the metadata dictionary/json.
        /// Important that this is descriptive and unique. It is used by the LLM to understand the content of the field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Display name of the field. Used for display in forms and other UI elements.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of the field. Shown as a tooltip in forms and other UI elements.
        /// </summary>
        public string? DescriptionToolTip { get; set; }

        /// <summary>
        /// Field type. Defines both the type of content present and how it should be presented.
        /// <see cref="DynamicDocumentProcessMetaDataFieldType"/>
        /// </summary>
        public DynamicDocumentProcessMetaDataFieldType FieldType { get; set; } =
            DynamicDocumentProcessMetaDataFieldType.Text;

        /// <summary>
        /// Whether the field is required for the document process. Triggers backend validation as well.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Order of the field in the form. Used for display purposes and to determined order of output in JSON.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Default value for the field. This must be convertible to the field type. Stored as a string.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Whether the field has a set of possible values. Used for dropdowns and other selection UI elements.
        /// Gets automatically set based on the presence of possible values.
        /// </summary>
        public bool HasPossibleValues { get; set; } = false;

        /// <summary>
        /// List of possible values for the field. Used for dropdowns and other selection UI elements.
        /// </summary>
        public List<string> PossibleValues { get; set; } = [];

        /// <summary>
        /// Default possible value for the field. Used for dropdowns and other selection UI elements.
        /// Must be a value in the PossibleValues list.
        /// </summary>
        public string? DefaultPossibleValue { get; set; }

        /// <summary>
        /// Creates a deep copy of the DocumentProcessMetadataFieldInfo.
        /// </summary>
        /// <returns>A new copy of this DocumentProfessMetadataFieldInfo</returns>
        public DocumentProcessMetadataFieldInfo Clone()
        {
            return new DocumentProcessMetadataFieldInfo
            {
                Id = this.Id,
                DynamicDocumentProcessDefinitionId = this.DynamicDocumentProcessDefinitionId,
                Name = this.Name,
                DisplayName = this.DisplayName,
                DescriptionToolTip = this.DescriptionToolTip,
                FieldType = this.FieldType,
                IsRequired = this.IsRequired,
                Order = this.Order,
                DefaultValue = this.DefaultValue,
                HasPossibleValues = this.HasPossibleValues,
                PossibleValues = [.. this.PossibleValues],
                DefaultPossibleValue = this.DefaultPossibleValue
            };
        }
    }
}