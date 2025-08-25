// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document reindexing operations.
/// </summary>
[Route("/api/reindex")]
public class DocumentReindexController : BaseController
{
    private readonly ILogger<DocumentReindexController> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentReindexController"/> class.
    /// </summary>
    /// <param name="logger">The logger for this controller.</param>
    /// <param name="clusterClient">Orleans Cluster client</param>
    /// <param name="documentProcessInfoService">The Document Process Info Service</param>
    /// <param name="documentLibraryInfoService">The Document Library Info Service</param>
    public DocumentReindexController(
        ILogger<DocumentReindexController> logger,
        IClusterClient clusterClient,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _logger = logger;
        _clusterClient = clusterClient;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
    }

    /// <summary>
    /// Starts reindexing for a document library.
    /// </summary>
    /// <param name="documentLibraryShortName">The short name of the document library.</param>
    /// <param name="reason">The reason for reindexing (e.g., "Chunk size changed").</param>
    /// <returns>
    /// Produces Status Codes:
    ///     202 Accepted: When reindexing is successfully started or already running
    ///     400 BadRequest: When the library doesn't support reindexing
    ///     404 NotFound: When the document library is not found
    /// </returns>
    [HttpPost("document-library/{documentLibraryShortName}")]
    [ProducesResponseType(typeof(ReindexStartResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartDocumentLibraryReindexing(string documentLibraryShortName, [FromBody] string reason = "Manual reindexing")
    {
        try
        {
            // Validate the document library exists and supports reindexing
            var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
            if (documentLibrary == null)
            {
                return NotFound($"Document library '{documentLibraryShortName}' not found.");
            }

            // Check if the library uses Semantic Kernel Vector Store
            if (documentLibrary.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
            {
                return BadRequest($"Reindexing is only supported for document libraries using SemanticKernelVectorStore logic type. Library '{documentLibraryShortName}' uses {documentLibrary.LogicType}.");
            }

            // Use a deterministic orchestration ID for idempotency/attach behavior
            var orchestrationId = $"library-{documentLibraryShortName}";
            var reindexGrain = _clusterClient.GetGrain<IDocumentReindexOrchestrationGrain>(orchestrationId);

            // If already running in this activation, just return the same ID so the UI can attach
            var existing = await reindexGrain.GetStateAsync();
            var isActive = await reindexGrain.IsRunningAsync();
            if (existing.Status == ReindexOrchestrationState.Running && isActive)
            {
                _logger.LogInformation("Reindexing already running for document library {DocumentLibraryName}. Orchestration ID: {OrchestrationId}",
                    documentLibraryShortName, orchestrationId);
                return Accepted(new ReindexStartResponse(orchestrationId, documentLibraryShortName, existing.Reason ?? reason));
            }

            // Start the reindexing process using fire-and-forget pattern (don't await)
            _ = reindexGrain.StartDocumentLibraryReindexingAsync(documentLibraryShortName, reason);

            _logger.LogInformation("Initiated reindexing for document library {DocumentLibraryName} with orchestration ID {OrchestrationId}",
                documentLibraryShortName, orchestrationId);

            return Accepted(new ReindexStartResponse(orchestrationId, documentLibraryShortName, reason));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting reindexing for document library {DocumentLibraryName}", documentLibraryShortName);
            return StatusCode(500, "An error occurred while starting the reindexing operation.");
        }
    }

    /// <summary>
    /// Starts reindexing for a document process.
    /// </summary>
    /// <param name="documentProcessShortName">The short name of the document process.</param>
    /// <param name="reason">The reason for reindexing (e.g., "Chunk size changed").</param>
    /// <returns>
    /// Produces Status Codes:
    ///     202 Accepted: When reindexing is successfully started or already running
    ///     400 BadRequest: When the process doesn't support reindexing
    ///     404 NotFound: When the document process is not found
    /// </returns>
    [HttpPost("document-process/{documentProcessShortName}")]
    [ProducesResponseType(typeof(ReindexStartResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartDocumentProcessReindexing(string documentProcessShortName, [FromBody] string reason = "Manual reindexing")
    {
        try
        {
            // Validate the document process exists and supports reindexing
            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessShortName);
            if (documentProcess == null)
            {
                return NotFound($"Document process '{documentProcessShortName}' not found.");
            }

            // Check if the process uses Semantic Kernel Vector Store
            if (documentProcess.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
            {
                return BadRequest($"Reindexing is only supported for document processes using SemanticKernelVectorStore logic type. Process '{documentProcessShortName}' uses {documentProcess.LogicType}.");
            }

            // Use a deterministic orchestration ID for idempotency/attach behavior
            var orchestrationId = $"process-{documentProcessShortName}";
            var reindexGrain = _clusterClient.GetGrain<IDocumentReindexOrchestrationGrain>(orchestrationId);

            // If already running in this activation, just return the same ID so the UI can attach
            var existing = await reindexGrain.GetStateAsync();
            var isActive = await reindexGrain.IsRunningAsync();
            if (existing.Status == ReindexOrchestrationState.Running && isActive)
            {
                _logger.LogInformation("Reindexing already running for document process {DocumentProcessName}. Orchestration ID: {OrchestrationId}",
                    documentProcessShortName, orchestrationId);
                return Accepted(new ReindexStartResponse(orchestrationId, documentProcessShortName, existing.Reason ?? reason));
            }

            // Start the reindexing process using fire-and-forget pattern (don't await)
            _ = reindexGrain.StartDocumentProcessReindexingAsync(documentProcessShortName, reason);

            _logger.LogInformation("Initiated reindexing for document process {DocumentProcessName} with orchestration ID {OrchestrationId}",
                documentProcessShortName, orchestrationId);

            return Accepted(new ReindexStartResponse(orchestrationId, documentProcessShortName, reason));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting reindexing for document process {DocumentProcessName}", documentProcessShortName);
            return StatusCode(500, "An error occurred while starting the reindexing operation.");
        }
    }

    /// <summary>
    /// Gets the status of a reindexing operation.
    /// </summary>
    /// <param name="orchestrationId">The orchestration ID of the reindexing operation.</param>
    /// <returns>
    /// The current status of the reindexing operation.
    /// Produces Status Codes:
    ///     200 OK: When the status is retrieved successfully
    ///     404 NotFound: When the orchestration is not found
    /// </returns>
    [HttpGet("status/{orchestrationId}")]
    [ProducesResponseType(typeof(DocumentReindexStateInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReindexingStatus(string orchestrationId)
    {
        try
        {
            var reindexGrain = _clusterClient.GetGrain<IDocumentReindexOrchestrationGrain>(orchestrationId);
            var grainState = await reindexGrain.GetStateAsync();

            if (string.IsNullOrEmpty(grainState.Id))
            {
                return NotFound($"Reindexing operation with orchestration ID '{orchestrationId}' not found.");
            }

            // Map grain state to contract DTO
            var stateInfo = new DocumentReindexStateInfo
            {
                Id = grainState.Id,
                DocumentLibraryShortName = grainState.DocumentLibraryShortName,
                DocumentLibraryType = grainState.DocumentLibraryType,
                TargetContainerName = grainState.TargetContainerName,
                Status = (ReindexOrchestrationState)grainState.Status,
                Reason = grainState.Reason,
                TotalDocuments = grainState.TotalDocuments,
                ProcessedDocuments = grainState.ProcessedDocuments,
                FailedDocuments = grainState.FailedDocuments,
                Errors = grainState.Errors,
                LastUpdatedUtc = grainState.LastUpdatedUtc,
                StartedUtc = grainState.StartedUtc,
                CompletedUtc = grainState.CompletedUtc
            };

            return Ok(stateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reindexing status for orchestration {OrchestrationId}", orchestrationId);
            return StatusCode(500, "An error occurred while retrieving the reindexing status.");
        }
    }
}