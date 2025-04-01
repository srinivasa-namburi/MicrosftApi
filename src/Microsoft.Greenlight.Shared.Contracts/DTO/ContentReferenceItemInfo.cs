using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Reference to a content type. Held only in cache for now.
    /// </summary>
    public class ContentReferenceItemInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the content reference item.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Reference to the content item if it is a reference type owned by the system (Generated Document, ContentNode etc).
        /// </summary>
        public Guid? ContentReferenceSourceId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the content reference item.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the type of the content reference.
        /// </summary>
        public ContentReferenceType ReferenceType { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the content reference item was created.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the description of the content reference item.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// UTC date and time when the entity was created.
        /// </summary>
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// UTC date and time when the entity was last modified.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; }
    }
}