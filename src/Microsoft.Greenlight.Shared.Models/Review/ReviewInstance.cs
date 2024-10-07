using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.Review;

public class ReviewInstance : EntityBase
{
    public required Guid ReviewDefinitionId { get; set; }
    [JsonIgnore]
    public ReviewDefinition? ReviewDefinition { get; set; }

    public required Guid ExportedLinkId { get; set; }
    [JsonIgnore]
    public ExportedDocumentLink? ExportedDocumentLink { get; set; }

    public string? ReviewDefinitionStateWhenSubmitted { get; set; }

    public ReviewInstanceStatus Status { get; set; } = ReviewInstanceStatus.Pending;

    public List<ReviewQuestionAnswer> ReviewQuestionAnswers { get; set; } = new();


}
