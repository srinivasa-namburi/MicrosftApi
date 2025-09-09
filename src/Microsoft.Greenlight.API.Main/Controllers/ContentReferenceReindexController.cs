// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.State;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;

namespace Microsoft.Greenlight.API.Main.Controllers;

[ApiController]
[Route("api/content-reference-reindex")] 
public class ContentReferenceReindexController : BaseController
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ContentReferenceReindexController> _logger;

    public ContentReferenceReindexController(IClusterClient clusterClient, ILogger<ContentReferenceReindexController> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// Starts reindexing for the specified ContentReferenceType.
    /// </summary>
    [HttpPost("{type}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Start(ContentReferenceType type, [FromBody] string reason = "Manual reindexing")
    {
        var orchestrationId = $"cr-reindex-{type}";
        var grain = _clusterClient.GetGrain<IContentReferenceReindexOrchestrationGrain>(orchestrationId);
        await grain.StartReindexingAsync(type, reason);
        return Accepted(new { OrchestrationId = orchestrationId, Type = type.ToString(), Reason = reason });
    }

    /// <summary>
    /// Gets current state for a content reference reindex orchestration.
    /// </summary>
    [HttpGet("{type}/state")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(typeof(ContentReferenceReindexState), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetState(ContentReferenceType type)
    {
        var orchestrationId = $"cr-reindex-{type}";
        var grain = _clusterClient.GetGrain<IContentReferenceReindexOrchestrationGrain>(orchestrationId);
        var state = await grain.GetStateAsync();
        return Ok(state);
    }
}
