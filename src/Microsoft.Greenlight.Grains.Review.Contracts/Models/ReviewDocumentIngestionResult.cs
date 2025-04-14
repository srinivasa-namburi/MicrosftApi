using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts.Models
{
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class ReviewDocumentIngestionResult
    {
        public Guid ExportedDocumentLinkId { get; set; }
        public int TotalNumberOfQuestions { get; set; }
    }
}