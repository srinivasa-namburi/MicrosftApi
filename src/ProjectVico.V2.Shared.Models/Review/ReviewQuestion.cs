using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models.Review;

public class ReviewQuestion : EntityBase
{
    public required string Question { get; set; }
    public string? Rationale { get; set; }
    public ReviewQuestionType QuestionType { get; set; }
    public Guid ReviewId { get; set; }
    [JsonIgnore]
    public ReviewDefinition? Review { get; set; }

}

public enum ReviewQuestionType  
{
    Question = 100,
    Requirement = 500
}