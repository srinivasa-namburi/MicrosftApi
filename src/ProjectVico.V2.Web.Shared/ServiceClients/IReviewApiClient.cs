using Microsoft.AspNetCore.Components.Forms;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IReviewApiClient : IServiceClient
{
    Task<List<ReviewDefinitionInfo>> GetAllReviews();
    Task<ReviewDefinitionInfo?> GetReviewById(Guid id);
    
    Task<ReviewDefinitionInfo?> CreateReview(ReviewDefinitionInfo reviewDefinitionInfo);
    Task<ReviewDefinitionInfo?> UpdateReview(Guid id, ReviewChangeRequest reviewDefinitionInfo);
    Task<bool?> DeleteReview(Guid id);

    Task<ReviewInstanceInfo?> GetReviewInstanceById(Guid id);
    Task<List<ReviewQuestionAnswerInfo>> GetReviewQuestionAnswersByReviewInstanceId(Guid reviewInstanceId);
    Task<ExportedDocumentLinkInfo> UploadDocumentForReviewInstanceAsync(IBrowserFile file);
    Task<ReviewInstanceInfo?> ExecuteReview(ReviewInstanceInfo id);
}