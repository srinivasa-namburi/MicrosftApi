using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class DocumentProcessApiClient : WebAssemblyBaseServiceClient<DocumentProcessApiClient>, IDocumentProcessApiClient
{
    public DocumentProcessApiClient(HttpClient httpClient, ILogger<DocumentProcessApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<DocumentProcessInfo>> GetAllDocumentProcessInfoAsync()
    {
        var url = "/api/document-processes";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that no document processes exist - return an empty list
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response?.EnsureSuccessStatusCode();

        // Return all DocumentInfo found
        var result = await response?.Content.ReadFromJsonAsync<List<DocumentProcessInfo>>()! ?? [];
        return result;
    }

    public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName)
    {
        var url = $"/api/document-processes/short-name/{shortName}";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that the document process does not exist - return null
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        // Return the DocumentInfo if found - otherwise return null
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessInfo>()! ?? null;
        return result;
    }

    public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid? id)
    {
        var url = $"/api/document-processes/{id}";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that the document process does not exist - return null
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        // Return the DocumentInfo if found - otherwise return null
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessInfo>()! ?? null;
        return result;
    }

    public async Task<bool> DeleteDocumentProcessAsync(Guid processId)
    {
        var url = $"/api/document-processes/{processId}";
        var response = await SendDeleteRequestMessage(url);

        response?.EnsureSuccessStatusCode();
        var result = await response?.Content.ReadFromJsonAsync<bool>()!;
        return result;
    }

    public async Task<DocumentProcessExportInfo?> ExportDocumentProcessByIdAsync(Guid processId)
    {
        var url = $"/api/document-process/{processId}/export";
        var response = await SendGetRequestMessage(url);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessExportInfo>()! ?? null;
        return result;
    }

    public async Task<DocumentProcessInfo?> CreateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo? documentProcessInfo)
    {
        var url = "/api/document-processes";
        var response = await SendPostRequestMessage(url, documentProcessInfo);
        
        response?.EnsureSuccessStatusCode();

        // Return the created DocumentInfo
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessInfo>()! ?? null;
        return result;
    }

    public async Task UpdateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo? documentProcessInfo)
    {
        var url = $"/api/document-processes/{documentProcessInfo.Id}";
        var response = await SendPutRequestMessage(url, documentProcessInfo);
        
        response?.EnsureSuccessStatusCode();
    }

    public async Task <List<DocumentProcessMetadataFieldInfo>> GetDocumentProcessMetadataFieldsAsync(Guid processId)
    {
        var url = $"/api/document-processes/{processId}/metadata-fields";
        var response = await SendGetRequestMessage(url);
        // If we get a 404, it means that no metadata fields exist - return an empty list
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }
        response?.EnsureSuccessStatusCode();
        // Return all DocumentInfo found
        var result = await response?.Content.ReadFromJsonAsync<List<DocumentProcessMetadataFieldInfo>>()! ?? [];
        return result;
    }

    public async Task<List<DocumentProcessMetadataFieldInfo>> StoreMetaDataFieldsForDocumentProcess(Guid processId, List<DocumentProcessMetadataFieldInfo> metadataFields)
    {
        var url = $"/api/document-processes/{processId}/metadata-fields";
        var response = await SendPostRequestMessage(url, metadataFields);
        response?.EnsureSuccessStatusCode();
        var result = await response?.Content.ReadFromJsonAsync<List<DocumentProcessMetadataFieldInfo>>()! ?? [];
        return result;
    }

    public async Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId)
    {
        var url = $"/api/prompts/by-process/{processId}";
        var response = await SendGetRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<PromptInfo>();
        }

        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<PromptInfo>>() ?? new List<PromptInfo>();
    }

    public async Task<PromptInfo> GetPromptByIdAsync(Guid id)
    {
        var url = $"/api/prompts/{id}";
        var response = await SendGetRequestMessage(url);

        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("Prompt not found");
        }

        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<PromptInfo>() ?? throw new KeyNotFoundException("Prompt not found");
    }

    public async Task<PromptInfo?> CreatePromptAsync(PromptInfo promptInfo)
    {
        var url = "/api/prompts";
        var response = await SendPostRequestMessage(url, promptInfo);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<PromptInfo>()!;
    }

    public async Task UpdatePromptAsync(PromptInfo promptInfo)
    {
        var url = $"/api/prompts/{promptInfo.Id}";
        // PUT generates a 400 error when proxied through YARP, so we use POST instead
        var response = await SendPutRequestMessage(url, promptInfo);
        response?.EnsureSuccessStatusCode();
    }

    public async Task<List<string>> GetRequiredPromptVariablesForPromptName(string promptName)
    {
        var url = $"/api/prompts/{promptName}/variables";
        var response = await SendGetRequestMessage(url);
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<string>();
        }
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<string>>()! ?? [];
    }

    public async Task<DocumentProcessValidationPipelineInfo?> GetValidationPipelineAsync(Guid documentProcessId)
    {
        var url = $"/api/document-processes/{documentProcessId}/validation-pipeline";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that the validation pipeline does not exist - return null
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        // Return the validation pipeline if found - otherwise return null
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessValidationPipelineInfo>()! ?? null;
        return result;
    }

    public async Task<DocumentProcessValidationPipelineInfo?> SaveValidationPipelineAsync(Guid documentProcessId, DocumentProcessValidationPipelineInfo pipelineInfo)
    {
        var url = $"/api/document-processes/{documentProcessId}/validation-pipeline";
        var response = await SendPostRequestMessage(url, pipelineInfo);
    
        response?.EnsureSuccessStatusCode();

        // Return the created/updated validation pipeline
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessValidationPipelineInfo>()! ?? null;
        return result;
    }

    public async Task<bool> DeleteValidationPipelineAsync(Guid documentProcessId)
    {
        var url = $"/api/document-processes/{documentProcessId}/validation-pipeline";
        var response = await SendDeleteRequestMessage(url);
    
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    
        response?.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> HasValidationPipelineAsync(string documentProcessShortName)
    {
        var url = $"/api/document-processes/short-name/{documentProcessShortName}/has-validation-pipeline";
        var response = await SendGetRequestMessage(url);
    
        if (!response!.IsSuccessStatusCode)
        {
            return false;
        }

        return await response.Content.ReadFromJsonAsync<bool>();
    }
}
