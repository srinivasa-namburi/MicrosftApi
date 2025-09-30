// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

/// <summary>
/// Client for Admin APIs to manage MCP configuration and sessions.
/// </summary>
public interface IMcpConfigurationApiClient : Microsoft.Greenlight.Web.Shared.ServiceClients.IServiceClient
{
    /// <summary>
    /// Gets the current MCP configuration model.
    /// </summary>
    Task<McpConfigurationModel> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the MCP configuration model.
    /// </summary>
    Task UpdateAsync(McpConfigurationModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active sessions.
    /// </summary>
    Task<List<McpSessionRow>> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates a session by id.
    /// </summary>
    Task InvalidateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all MCP API secrets (no plaintext values).
    /// </summary>
    Task<List<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.McpSecretInfo>> ListSecretsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new MCP API secret and returns its plaintext once.
    /// </summary>
    Task<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretResponse> CreateSecretAsync(Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an API secret by id.
    /// </summary>
    Task DeleteSecretAsync(Guid id, CancellationToken cancellationToken = default);
}
