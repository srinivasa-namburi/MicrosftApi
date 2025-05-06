using System;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts.Models
{
    /// <summary>
    /// Represents the content type for a review
    /// </summary>
    public enum ReviewContentType
    {
        /// <summary>
        /// No document is associated with this review
        /// </summary>
        NoDocument,
        
        /// <summary>
        /// An external file is used for this review
        /// </summary>
        ExternalFile,
        
        /// <summary>
        /// Content from a document process is used for this review
        /// </summary>
        DocumentProcess
    }
    
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    /// <summary>
    /// Contains information about the result of a document ingestion operation for reviews
    /// </summary>
    public class ReviewDocumentIngestionResult
    {
        /// <summary>
        /// Gets or sets the ID of the exported document link.
        /// </summary>
        public Guid? ExportedDocumentLinkId { get; set; }
        
        /// <summary>
        /// Gets or sets the total number of questions in the review.
        /// </summary>
        public int TotalNumberOfQuestions { get; set; }
        
        /// <summary>
        /// Gets or sets the short name of the document process used.
        /// </summary>
        public string DocumentProcessShortName { get; set; }
        
        /// <summary>
        /// Gets or sets the type of content for this review
        /// </summary>
        public ReviewContentType ContentType { get; set; }
    }
}