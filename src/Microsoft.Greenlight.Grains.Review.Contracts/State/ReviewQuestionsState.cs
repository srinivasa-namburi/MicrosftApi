using Microsoft.Greenlight.Shared.Contracts.DTO;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts.State
{
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class ReviewQuestionsState
    {
        public List<ReviewQuestionInfo> Questions { get; set; } = new List<ReviewQuestionInfo>();
    }
}