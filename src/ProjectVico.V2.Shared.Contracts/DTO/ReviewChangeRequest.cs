namespace ProjectVico.V2.Shared.Contracts.DTO;

public class ReviewChangeRequest
{
    public required ReviewDefinitionInfo ReviewDefinition { get; set; }
    public List<ReviewQuestionInfo> ChangedOrAddedQuestions { get; set; } = [];
    public List<ReviewQuestionInfo> DeletedQuestions { get; set; } = [];
}