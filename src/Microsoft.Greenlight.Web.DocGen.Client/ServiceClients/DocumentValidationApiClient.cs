using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class DocumentValidationApiClient : WebAssemblyBaseServiceClient<DocumentValidationApiClient>, IDocumentValidationApiClient
{
    public DocumentValidationApiClient(HttpClient httpClient, ILogger<DocumentValidationApiClient> logger, AuthenticationStateProvider authStateProvider) 
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<bool> StartDocumentValidationAsync(string documentId)
    {
        var response = await SendPostRequestMessage($"/api/document-validation/{documentId}", null);
        // This returns the validation execution id, but we're not currently keeping it.
        return response?.IsSuccessStatusCode ?? false;
    }

    public async Task<DocumentProcessValidationPipelineInfo?> GetValidationPipelineConfigurationByProcessNameAsync(string processName)
    {
        if (string.IsNullOrEmpty(processName))
        {
            return null;
        }

        var response = await SendGetRequestMessage($"/api/document-validation/process/{processName}/validation-pipeline");
    
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    
        response?.EnsureSuccessStatusCode();
    
        return await response?.Content.ReadFromJsonAsync<DocumentProcessValidationPipelineInfo>();
    }

    public async Task<ValidationStatusInfo> GetDocumentValidationStatusAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/document-validation/{documentId}");
        response?.EnsureSuccessStatusCode();
        
        return await response?.Content.ReadFromJsonAsync<ValidationStatusInfo>()! ?? 
               throw new IOException("Failed to get validation status!");
    }

    public async Task<ValidationResultsInfo?> GetLatestValidationResultsAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/document-validation/{documentId}/latest-results");

        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ValidationResultsInfo>()!;
    }

    public async Task<bool> UpdateValidationApplicationStatusAsync(
        string validationExecutionId, 
        ValidationPipelineExecutionApplicationStatus status)
    {
        var response = await SendPutRequestMessage(
            $"/api/document-validation/{validationExecutionId}/application-status/{(int)status}", 
            null);
            
        return response?.IsSuccessStatusCode ?? false;
    }

    /// <summary>
    /// Updates the application status of a specific validation content change.
    /// </summary>
    /// <param name="contentChangeId">The ID of the validation content change.</param>
    /// <param name="status">The new application status.</param>
    /// <returns>True if the update was successful, otherwise false.</returns>
    public async Task<bool> UpdateValidationContentChangeStatusAsync(
        Guid contentChangeId,
        ValidationContentNodeApplicationStatus status)
    {
        var response = await SendPutRequestMessage(
            $"/api/document-validation/content-change/{contentChangeId}/application-status/{(int)status}",
            null);

        return response?.IsSuccessStatusCode ?? false;
    }
}
