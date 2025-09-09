// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public sealed class AdminAuthorizationApiClient : WebAssemblyBaseServiceClient<AdminAuthorizationApiClient>, IAdminAuthorizationApiClient
{
    public AdminAuthorizationApiClient(HttpClient httpClient, ILogger<AdminAuthorizationApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    /// <summary>
    /// Debug method to get authorization details for the current user.
    /// </summary>
    public async Task<AuthorizationDebugInfo> GetAuthorizationDebugInfoAsync()
    {
        try
        {
            var resp = await SendGetRequestMessage("/api/adminauthorization/debug", authorize: true);
            resp!.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<AuthorizationDebugInfo>() ?? new AuthorizationDebugInfo { ProviderSubjectId = string.Empty };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting authorization debug info");
            throw;
        }
    }

    public async Task<List<PermissionInfo>> ListPermissionsAsync()
    {
        var resp = await SendGetRequestMessage("/api/adminauthorization/permissions", authorize: true);
        resp!.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<PermissionInfo>>() ?? [];
    }

    public async Task<List<RoleInfo>> ListRolesAsync()
    {
        var resp = await SendGetRequestMessage("/api/adminauthorization/roles", authorize: true);
        resp!.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<RoleInfo>>() ?? [];
    }

    /// <summary>
    /// Checks if the current user has permission to access administrative authorization features.
    /// This is a lightweight check that doesn't return data, just permission status.
    /// </summary>
    public async Task<bool> CanAccessAdminAuthorizationAsync()
    {
        try
        {
            Logger.LogInformation("Starting authorization check for admin authorization access");
            
            var resp = await SendGetRequestMessage("/api/adminauthorization/permissions", authorize: true);
            
            Logger.LogInformation("Received response from /api/adminauthorization/permissions. StatusCode: {StatusCode}, IsSuccessStatusCode: {IsSuccessStatusCode}", 
                resp?.StatusCode, resp?.IsSuccessStatusCode);

            if (resp?.IsSuccessStatusCode == true)
            {
                // Try to read the content to ensure it's actually valid
                var content = await resp.Content.ReadFromJsonAsync<List<PermissionInfo>>();
                Logger.LogInformation("Successfully parsed permissions response. Permission count: {PermissionCount}", content?.Count ?? 0);
                return true;
            }
            else
            {
                Logger.LogWarning("Authorization check failed. StatusCode: {StatusCode}", resp?.StatusCode);
                
                // Log additional debug info
                try
                {
                    var debugInfo = await GetAuthorizationDebugInfoAsync();
                    Logger.LogInformation("Authorization debug info: {@DebugInfo}", debugInfo);
                }
                catch (Exception debugEx)
                {
                    Logger.LogWarning(debugEx, "Could not retrieve authorization debug info");
                }
                
                return false;
            }
        }
        catch (HttpRequestException httpEx)
        {
            Logger.LogWarning("HTTP error during authorization check: {Message}", httpEx.Message);
            return false;
        }
        catch (UnauthorizedAccessException unauthorizedEx)
        {
            Logger.LogInformation("User does not have permission for admin authorization: {Message}", unauthorizedEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during authorization check: {ExceptionType} - {Message}", 
                ex.GetType().Name, ex.Message);
            return false;
        }
    }

    public async Task<RoleInfo> UpsertRoleAsync(UpsertRoleRequest request)
    {
        var resp = await SendPostRequestMessage("/api/adminauthorization/roles", request, authorize: true);
        await EnsureSuccessWithValidationAsync(resp!, "Role operation failed", "role upsert");
        
        var saved = await resp!.Content.ReadFromJsonAsync<RoleInfo>();
        return saved!;
    }

    public async Task SetRolePermissionsAsync(SetRolePermissionsRequest request)
    {
        var resp = await SendPostRequestMessage("/api/adminauthorization/roles/permissions", request, authorize: true);
        await EnsureSuccessWithValidationAsync(resp!, "Permission update failed", "role permissions update");
    }

    public async Task AssignUserRoleAsync(AssignUserRoleRequest request)
    {
        var resp = await SendPostRequestMessage("/api/adminauthorization/assignments", request, authorize: true);
        await EnsureSuccessWithValidationAsync(resp!, "Role assignment failed", "role assignment");
    }

    public async Task RemoveAssignmentAsync(string providerSubjectId, Guid roleId)
    {
        var resp = await SendDeleteRequestMessage($"/api/adminauthorization/assignments/{providerSubjectId}/{roleId}", authorize: true);
        await EnsureSuccessWithValidationAsync(resp, "Role assignment removal failed", "role assignment removal");
    }

    public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
    {
        var q = Uri.EscapeDataString(query ?? string.Empty);
        var resp = await SendGetRequestMessage($"/api/adminauthorization/users/search?q={q}", authorize: true);
        resp!.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<UserSearchResult>>() ?? [];
    }

    public async Task DeleteRoleAsync(Guid roleId)
    {
        var resp = await SendDeleteRequestMessage($"/api/adminauthorization/roles/{roleId}", authorize: true);
        await EnsureSuccessWithValidationAsync(resp, "Role deletion failed", "role deletion");
    }

    public async Task<List<EntraRoleMappingInfo>> ListEntraRoleMappingsAsync()
    {
        var resp = await SendGetRequestMessage("/api/adminauthorization/entra-roles", authorize: true);
        resp!.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<EntraRoleMappingInfo>>() ?? [];
    }

    public async Task UpsertEntraRoleMappingAsync(UpsertEntraRoleMappingRequest request)
    {
        var resp = await SendPostRequestMessage("/api/adminauthorization/entra-roles", request, authorize: true);
        await EnsureSuccessWithValidationAsync(resp!, "Entra role mapping failed", "entra role mapping");
    }

    /// <summary>
    /// Helper method to handle HTTP response validation and extract validation error messages from BadRequest responses.
    /// </summary>
    /// <param name="response">The HTTP response to validate.</param>
    /// <param name="defaultErrorMessage">The default error message if validation error extraction fails.</param>
    /// <param name="operationName">The name of the operation for logging purposes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the response indicates failure with the extracted or default error message.</exception>
    private async Task EnsureSuccessWithValidationAsync(HttpResponseMessage response, string defaultErrorMessage, string operationName)
    {
        if (response.IsSuccessStatusCode)
        {
            return; // Success
        }
        
        var errorMessage = defaultErrorMessage;
        
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            try
            {
                // The validation error message is sent as plain text in the response body
                var validationError = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    errorMessage = validationError;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not read validation error from response during {OperationName}: {Exception}", operationName, ex.Message);
            }
        }
        
        Logger.LogWarning("HTTP error during {OperationName}. Status: {StatusCode}, Message: {ErrorMessage}", operationName, response.StatusCode, errorMessage);
        throw new InvalidOperationException(errorMessage);
    }
}
