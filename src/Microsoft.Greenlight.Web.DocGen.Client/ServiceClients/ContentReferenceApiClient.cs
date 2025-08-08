// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;
using System.Text.Json;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients
{
    public class ContentReferenceApiClient : WebAssemblyBaseServiceClient<ContentReferenceApiClient>, IContentReferenceApiClient
    {
        public ContentReferenceApiClient(HttpClient httpClient, ILogger<ContentReferenceApiClient> logger, AuthenticationStateProvider authStateProvider)
            : base(httpClient, logger, authStateProvider)
        {
        }

        public async Task<List<ContentReferenceItemInfo>> GetAllReferencesAsync()
        {
            try
            {
                var response = await SendGetRequestMessage("/api/content-references/all");
                
                if (response == null)
                {
                    Logger.LogWarning("GetAllReferencesAsync: Received null response");
                    return new List<ContentReferenceItemInfo>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("GetAllReferencesAsync: API returned {StatusCode}", response.StatusCode);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.LogError("GetAllReferencesAsync: Error content: {ErrorContent}", errorContent);
                    return new List<ContentReferenceItemInfo>();
                }

                // Read the raw content first for debugging
                var rawContent = await response.Content.ReadAsStringAsync();
                Logger.LogDebug("GetAllReferencesAsync: Raw response content (first 500 chars): {RawContent}", 
                    rawContent.Length > 500 ? rawContent.Substring(0, 500) + "..." : rawContent);

                // Check if content is empty or null
                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    Logger.LogWarning("GetAllReferencesAsync: Response content is empty or whitespace");
                    return new List<ContentReferenceItemInfo>();
                }

                // Try to deserialize with better error information
                try
                {
                    // Use JsonSerializer with explicit options to better handle potential issues
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    };
                    
                    var result = JsonSerializer.Deserialize<List<ContentReferenceItemInfo>>(rawContent, options);
                    Logger.LogInformation("GetAllReferencesAsync: Successfully deserialized {Count} references", result?.Count ?? 0);
                    return result ?? new List<ContentReferenceItemInfo>();
                }
                catch (JsonException jsonEx)
                {
                    Logger.LogError(jsonEx, "GetAllReferencesAsync: JSON deserialization failed. Raw content length: {Length}, Content start: {ContentStart}", 
                        rawContent.Length, rawContent.Length > 100 ? rawContent.Substring(0, 100) : rawContent);
                    
                    // Try to determine if it's an array vs object vs other
                    var trimmedContent = rawContent.Trim();
                    if (trimmedContent.StartsWith("{"))
                    {
                        Logger.LogError("GetAllReferencesAsync: Response appears to be an object, not an array. Full content: {FullContent}", rawContent);
                    }
                    else if (trimmedContent.StartsWith("["))
                    {
                        Logger.LogError("GetAllReferencesAsync: Response appears to be an array but deserialization failed. Full content: {FullContent}", rawContent);
                    }
                    else
                    {
                        Logger.LogError("GetAllReferencesAsync: Response doesn't appear to be valid JSON. Full content: {FullContent}", rawContent);
                    }
                    
                    return new List<ContentReferenceItemInfo>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAllReferencesAsync: Unexpected error occurred");
                return new List<ContentReferenceItemInfo>();
            }
        }

        public async Task<List<ContentReferenceItemInfo>> SearchReferencesAsync(string term)
        {
            try
            {
                var response = await SendGetRequestMessage($"/api/content-references/search?term={term}");
                
                if (response == null || !response.IsSuccessStatusCode)
                {
                    Logger.LogError("SearchReferencesAsync: API call failed with status {StatusCode}", response?.StatusCode);
                    return new List<ContentReferenceItemInfo>();
                }

                var rawContent = await response.Content.ReadAsStringAsync();
                Logger.LogDebug("SearchReferencesAsync: Raw response content (first 200 chars): {RawContent}", 
                    rawContent.Length > 200 ? rawContent.Substring(0, 200) + "..." : rawContent);

                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    Logger.LogWarning("SearchReferencesAsync: Response content is empty or whitespace");
                    return new List<ContentReferenceItemInfo>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var result = JsonSerializer.Deserialize<List<ContentReferenceItemInfo>>(rawContent, options);
                return result ?? new List<ContentReferenceItemInfo>();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SearchReferencesAsync: Error occurred while searching references");
                return new List<ContentReferenceItemInfo>();
            }
        }

        public async Task<ContentReferenceItemInfo> GetReferenceByIdAsync(Guid id, ContentReferenceType type)
        {
            try
            {
                var response = await SendGetRequestMessage($"/api/content-references/{id}/{type}");
                
                if (response == null || !response.IsSuccessStatusCode)
                {
                    Logger.LogError("GetReferenceByIdAsync: API call failed with status {StatusCode}", response?.StatusCode);
                    return new ContentReferenceItemInfo();
                }

                var rawContent = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    Logger.LogWarning("GetReferenceByIdAsync: Response content is empty or whitespace");
                    return new ContentReferenceItemInfo();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var result = JsonSerializer.Deserialize<ContentReferenceItemInfo>(rawContent, options);
                return result ?? new ContentReferenceItemInfo();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetReferenceByIdAsync: Error occurred while getting reference {Id} of type {Type}", id, type);
                return new ContentReferenceItemInfo();
            }
        }

        public async Task RefreshReferenceCacheAsync()
        {
            var response = await SendPostRequestMessage("/api/content-references/refresh", null);
            response?.EnsureSuccessStatusCode();
        }

        public async Task<bool> RemoveReferenceAsync(Guid referenceId, Guid conversationId)
        {
            var response = await SendDeleteRequestMessage($"/api/content-references/remove/{referenceId}/{conversationId}");
            // If controller returns 404, return false; otherwise ensure success.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }
    }
}