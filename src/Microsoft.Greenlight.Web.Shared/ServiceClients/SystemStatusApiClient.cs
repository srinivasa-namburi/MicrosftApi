// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public class SystemStatusApiClient : CrossPlatformServiceClientBase<SystemStatusApiClient>, ISystemStatusApiClient
{
    public SystemStatusApiClient(HttpClient httpClient, ILogger<SystemStatusApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<SystemStatusSnapshot> GetSystemStatusAsync()
    {
        var response = await SendGetRequestMessage("/api/system-status", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<SystemStatusSnapshot>()! ??
               throw new IOException("No system status!");
    }

    public async Task<SubsystemStatus?> GetSubsystemStatusAsync(string source)
    {
        var response = await SendGetRequestMessage($"/api/system-status/subsystem/{source}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<SubsystemStatus?>();
    }
}