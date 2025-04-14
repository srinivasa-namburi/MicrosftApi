using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models
{
    /// <summary>
    /// Represents stored embeddings for content chunks to avoid regenerating them
    /// </summary>
    public class ContentEmbedding : EntityBase
    {
        /// <summary>
        /// The source content reference item ID that this embedding relates to
        /// </summary>
        public Guid ContentReferenceItemId { get; set; }

        /// <summary>
        /// Navigation property to the content reference item
        /// </summary>

        [JsonIgnore]
        public virtual ContentReferenceItem? ContentReferenceItem { get; set; }

        /// <summary>
        /// The text chunk that was embedded
        /// </summary>
        public string ChunkText { get; set; }

        /// <summary>
        /// The embedding vector serialized as a byte array
        /// </summary>
        public byte[] EmbeddingVector { get; set; }

        /// <summary>
        /// The sequence number of this chunk within the source content
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// When the embedding was last generated
        /// </summary>
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    }
}