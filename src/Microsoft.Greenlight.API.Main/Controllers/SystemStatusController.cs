// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Orleans;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// API controller for system status monitoring operations.
/// </summary>
[ApiController]
[Route("api/system-status")]
[Authorize]
public class SystemStatusController : BaseController
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SystemStatusController> _logger;

    public SystemStatusController(IClusterClient clusterClient, ILogger<SystemStatusController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current comprehensive system status snapshot.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SystemStatusSnapshot>> GetSystemStatusAsync()
    {
        try
        {
            var aggregatorGrain = _clusterClient.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
            var systemStatus = await aggregatorGrain.GetSystemStatusAsync();
            return Ok(systemStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system status");
            return StatusCode(500, "Failed to get system status");
        }
    }

    /// <summary>
    /// Gets the current status for a specific subsystem.
    /// </summary>
    /// <param name="source">The subsystem source (e.g., "VectorStore", "WorkerThreads", "Ingestion")</param>
    [HttpGet("subsystem/{source}")]
    public async Task<ActionResult<SubsystemStatus?>> GetSubsystemStatusAsync(string source)
    {
        try
        {
            var aggregatorGrain = _clusterClient.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
            var subsystemStatus = await aggregatorGrain.GetSubsystemStatusAsync(source);
            return Ok(subsystemStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subsystem status for {Source}", source);
            return StatusCode(500, "Failed to get subsystem status");
        }
    }
}