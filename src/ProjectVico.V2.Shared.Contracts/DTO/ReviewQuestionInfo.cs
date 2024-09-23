using System.Reflection.Emit;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Contracts.DTO;

public class ReviewQuestionInfo
{
    public required Guid Id { get; set; } = Guid.NewGuid();
    public required string Question { get; set; }
    public string? Rationale { get; set; }
    public required Guid ReviewId { get; set; }
    public ReviewQuestionType QuestionType { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not ReviewQuestionInfo other)
            return false;

        return Id == other.Id &&
               Question == other.Question &&
               Rationale == other.Rationale &&
               ReviewId == other.ReviewId &&
               QuestionType == other.QuestionType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Question, Rationale, ReviewId, QuestionType);
    }
}