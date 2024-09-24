using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.Client.ServiceClients;

public class ReviewApiClient : WebAssemblyBaseServiceClient<ReviewApiClient>, IReviewApiClient
{
    public ReviewApiClient(
        HttpClient httpClient, 
        ILogger<ReviewApiClient> logger, 
        AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<ReviewDefinitionInfo>> GetAllReviews()
    {
        var url = "/api/review";
        var response = await SendGetRequestMessage(url);

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<ReviewDefinitionInfo>>()! ??
               throw new IOException("No documents!");
    }

    public async Task<ReviewDefinitionInfo?> GetReviewById(Guid id)
    {
        var url = $"/api/review/{id}";
        var response = await SendGetRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ReviewDefinitionInfo>()!;
    }

    public async Task<ReviewDefinitionInfo?> CreateReview(ReviewDefinitionInfo reviewDefinitionInfo)
    {
        var url = "/api/review";
        var response = await SendPostRequestMessage(url, reviewDefinitionInfo);

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ReviewDefinitionInfo>()!;
    }

    public async Task<ReviewDefinitionInfo?> UpdateReview(Guid id, ReviewChangeRequest reviewDefinitionInfo)
    {
        var url = $"/api/review/{id}";
        var response = await SendPutRequestMessage(url, reviewDefinitionInfo);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ReviewDefinitionInfo>()!;
    }

    public async Task<bool?> DeleteReview(Guid id)
    {
        var url = $"/api/review/{id}";
        var response = await SendDeleteRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return false;
        }

        return await response.Content.ReadFromJsonAsync<bool>();
    }

    public async Task<ReviewInstanceInfo?> ExecuteReview(ReviewInstanceInfo reviewInstance)
    {
        if (reviewInstance.Id == Guid.Empty)
        {
            return null;
        }

        var url = $"/api/review-instance/{reviewInstance.Id}/execute";
        var response = await SendPostRequestMessage(url, null);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            // If we get a 404, the review instance does not exist, so we can't execute it directly. 
            // Create a review instance and then re-call this method.

            url = "/api/review-instance";
            response = await SendPostRequestMessage(url, reviewInstance);

            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            try
            {
                response?.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                return null;
            }

            reviewInstance = await response.Content.ReadFromJsonAsync<ReviewInstanceInfo>();
            return await ExecuteReview(reviewInstance);
        }

        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return null;
        }
        var result = await response.Content.ReadFromJsonAsync<ReviewInstanceInfo>();
        return result;
    }

    public async Task<ReviewInstanceInfo?> GetReviewInstanceById(Guid id)
    {
        var url = $"/api/review-instance/{id}";
        var response = await SendGetRequestMessage(url);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ReviewInstanceInfo>();
    }

    public async Task<List<ReviewQuestionAnswerInfo>> GetReviewQuestionAnswersByReviewInstanceId(Guid reviewInstanceId)
    {
        var url = $"/api/review-instance/{reviewInstanceId}/answers";
        var response = await SendGetRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound || response == null)
        {
            return [];
        }

        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return [];
        }
        
        return await response.Content.ReadFromJsonAsync<List<ReviewQuestionAnswerInfo>>();
    }

    public async Task<ExportedDocumentLinkInfo> UploadDocumentForReviewInstanceAsync(IBrowserFile file)
    {
        var fileName = file.Name;
        var url = $"/api/file/upload/reviews/{fileName}/file-info";

        var response = await SendPostRequestMessage(url, file);

        response?.EnsureSuccessStatusCode();

        // Execute a GET request to get the ExportedDocumentLinkInfo
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();
        var fileInfo = await response?.Content.ReadFromJsonAsync<ExportedDocumentLinkInfo>()!;
        return fileInfo;
        
    }
}