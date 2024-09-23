namespace ProjectVico.V2.Shared.Contracts.DTO;

public class ReviewDefinitionInfo
{
    public required Guid Id { get; set; } = Guid.NewGuid();
    public List<ReviewQuestionInfo> ReviewQuestions { get; set; } = new();
    public required string Title { get; set; }
    public string? Description { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not ReviewDefinitionInfo other)
            return false;

        return Id == other.Id;
    }

    public override string ToString()
    {
        return Title ?? string.Empty;
    }
}