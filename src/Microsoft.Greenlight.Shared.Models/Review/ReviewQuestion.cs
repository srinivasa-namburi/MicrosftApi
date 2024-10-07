using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.Review;

public class ReviewQuestion : EntityBase
{
    public required string Question { get; set; }
    public string? Rationale { get; set; }
    public ReviewQuestionType QuestionType { get; set; }
    public Guid ReviewId { get; set; }
    [JsonIgnore]
    public ReviewDefinition? Review { get; set; }

}
