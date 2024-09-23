using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;
using System.Net;
using Microsoft.AspNetCore.Components.Forms;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

public class ReviewApiClient : BaseServiceClient<ReviewApiClient>, IReviewApiClient
{
    public ReviewApiClient(HttpClient httpClient, ILogger<ReviewApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
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

    public async Task<ReviewInstanceInfo?> ExecuteReview(ReviewInstanceInfo id)
    {
        var url = $"/api/review-instance/{id}/execute";
        var response = await SendPostRequestMessage(url, null);

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

        if (response?.StatusCode == HttpStatusCode.NotFound)
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
        var url = $"/api/file/upload/reviews/{fileName}";

        var response = await SendPostRequestMessage(url, file);

        response?.EnsureSuccessStatusCode();

        // Execute a GET request to get the ExportedDocumentLinkInfo
        var fileAccessUrl = await response?.Content.ReadAsStringAsync();

        // Encode the fileAccessUrl
        fileAccessUrl = WebUtility.UrlEncode(fileAccessUrl);

        url = $"/api/file/file-info/{fileAccessUrl}";
        response = await SendGetRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<ExportedDocumentLinkInfo>()!;
    }



}