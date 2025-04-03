using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models
{
    /// <summary>
    /// Reference to a content type. Used for backend services and Entity Framework.
    /// </summary>
    public class ContentReferenceItem : EntityBase
    {
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
        /// Gets or sets the description of the content reference item.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the RAG text for the content reference item.
        /// </summary>
        public string? RagText { get; set; }

        /// <summary>
        /// Hash of the file content for ExternalFile type references
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// Generated Embeddings, if any, for this content reference item
        /// </summary>
        public List<ContentEmbedding> Embeddings { get; set; } = [];
    }
}