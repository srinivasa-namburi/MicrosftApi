using AutoMapper;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.Greenlight.Shared.Services;


namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document validation operations.
/// </summary>
[Route("/api/document-validation")]
public class DocumentValidationController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<DocumentValidationController> _logger;
    private readonly IClusterClient _clusterClient;
    private IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IContentNodeService _contentNodeService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentValidationController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for document generation.</param>
    /// <param name="logger">The logger for this controller.</param>
    /// <param name="clusterClient">Orleans Cluster client</param>
    /// <param name="documentProcessInfoService">The Document Process Info Service</param>
    /// <param name="contentNodeService">Content Node Service</param>
    /// <param name="mapper">AutoMapper</param>
    public DocumentValidationController(
        DocGenerationDbContext dbContext,
        ILogger<DocumentValidationController> logger,
        IClusterClient clusterClient,
        IDocumentProcessInfoService documentProcessInfoService,
        IContentNodeService contentNodeService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _logger = logger;
        _clusterClient = clusterClient;
        _documentProcessInfoService = documentProcessInfoService;
        _contentNodeService = contentNodeService;
        _mapper = mapper;
    }

    /// <summary>
    /// Starts validation for a document.
    /// </summary>
    /// <param name="documentId">The ID of the document to validate.</param>
    /// <returns>
    /// Produces Status Codes:
    ///     202 Accepted: When validation is successfully started
    ///     404 NotFound: When the document is not found
    ///     400 BadRequest: When validation cannot be started
    /// </returns>
    [HttpPost("{documentId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartDocumentValidation(Guid documentId)
    {
        // Check if document exists
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            return NotFound($"Document with ID {documentId} not found.");
        }

        // Check if document has a process with a validation pipeline
        var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
            .AsNoTracking()
            .Include(d => d.ValidationPipeline)
            .FirstOrDefaultAsync(d => d.ShortName == document.DocumentProcess);

        if (documentProcess?.ValidationPipeline == null)
        {
            return BadRequest("No validation pipeline found for this document's process.");
        }

        try
        {
            // Use the validation starter grain instead of sending a message directly
            var validationStarterGrain = _clusterClient.GetGrain<IValidationStarterGrain>(documentId);
            _ = validationStarterGrain.StartValidationForDocumentAsync(documentId);

            _logger.LogInformation("Published validation request for document {DocumentId}", documentId);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting validation for document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while starting the validation.");
        }
    }

    /// <summary>
    /// Gets validation status for a document.
    /// </summary>
    /// <param name="documentId">The ID of the document to check.</param>
    /// <returns>
    /// Produces Status Codes:
    ///     200 OK: When validation status is retrieved successfully
    ///     404 NotFound: When no validation exists for the document
    /// </returns>
    [HttpGet("{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> GetDocumentValidationStatus(Guid documentId)
    {
        // Find the most recent validation execution for this document
        var validation = await _dbContext.ValidationPipelineExecutions
            .AsNoTracking()
            .Include(v => v.ExecutionSteps)
            .Where(v => v.GeneratedDocumentId == documentId)
            .OrderByDescending(v => v.CreatedUtc)
            .FirstOrDefaultAsync();

        if (validation == null)
        {
            return NotFound($"No validation found for document {documentId}");
        }

        // Map to the DTO
        var steps = validation.ExecutionSteps
            .OrderBy(s => s.Order)
            .Select(s => new ValidationStepStatusInfo
            {
                StepId = s.Id,
                Order = s.Order,
                ExecutionType = s.PipelineExecutionType.ToString(),
                Status = s.PipelineExecutionStepStatus.ToString()
            })
            .ToList();

        var status = new ValidationStatusInfo
        {
            DocumentId = documentId,
            ValidationId = validation.Id,
            Started = validation.CreatedUtc,
            Steps = steps
        };

        return Ok(status);
    }

    /// <summary>
    /// Gets the validation pipeline configuration for a document process by its short name.
    /// </summary>
    /// <param name="processName">The short name of the document process.</param>
    /// <returns>
    /// The validation pipeline configuration.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    ///     404 Not found: When the document process or its validation pipeline is not found
    /// </returns>
    [HttpGet("process/{processName}/validation-pipeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DocumentProcessValidationPipelineInfo>]
    public async Task<IActionResult> GetValidationPipelineConfigurationByProcessName(string processName)
    {
        // First, get the document process by its short name
        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(processName);

        if (documentProcess == null)
        {
            return NotFound($"Document process '{processName}' not found.");
        }

        // Now get the validation pipeline for this process
        var validationPipeline = await _dbContext.DocumentProcessValidationPipelines
            .Include(p => p.ValidationPipelineSteps)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DocumentProcessId == documentProcess.Id);

        if (validationPipeline == null)
        {
            return NotFound($"No validation pipeline found for document process '{processName}'.");
        }

        // Map to DTO
        var pipelineInfo = _mapper.Map<DocumentProcessValidationPipelineInfo>(validationPipeline);
        return Ok(pipelineInfo);
    }


    /// <summary>
    /// Gets the latest completed validation results with recommended changes for a document.
    /// </summary>
    /// <param name="documentId">The ID of the document</param>
    /// <returns>
    /// The validation results with recommended changes if any.
    /// Produces Status Codes:
    ///     200 OK: When results are found
    ///     404 NotFound: When no completed validation with changes exists
    /// </returns>
    [HttpGet("{documentId}/latest-results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> GetLatestValidationResults(Guid documentId)
    {
        // Look for the most recent completed validation execution for this document
        var latestValidation = await _dbContext.ValidationPipelineExecutions
            .AsNoTracking()
            .Include(x => x.ExecutionSteps)
                .ThenInclude(s => s.ValidationPipelineExecutionStepResult)
                    .ThenInclude(r => r!.ContentNodeResults)
            .Where(x => x.GeneratedDocumentId == documentId)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();

        if (latestValidation == null)
        {
            return NotFound($"No validation executions found for document {documentId}");
        }

        // Check if all steps are completed
        bool isCompleted = latestValidation.ExecutionSteps.All(s =>
            s.PipelineExecutionStepStatus == ValidationPipelineExecutionStepStatus.Done);

        if (!isCompleted)
        {
            return NotFound($"No completed validation executions found for document {documentId}");
        }

        // Collect all content node changes
        var contentChanges = new List<ValidationContentChangeInfo>();
        foreach (var step in latestValidation.ExecutionSteps)
        {
            if (step.ValidationPipelineExecutionStepResult == null)
            {
                continue;
            }

            foreach (var contentNodeResult in step.ValidationPipelineExecutionStepResult.ContentNodeResults
                         .Where(contentNodeResult => contentNodeResult.OriginalContentNodeId != Guid.Empty &&
                                                     contentNodeResult.ResultantContentNodeId != Guid.Empty &&
                                                     contentNodeResult.ApplicationStatus != ValidationContentNodeApplicationStatus.NoChangesRecommended))
            {
                // Get the original and resultant content nodes
                var originalNode = await _dbContext.ContentNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == contentNodeResult.OriginalContentNodeId);

                var resultantNode = await _dbContext.ContentNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == contentNodeResult.ResultantContentNodeId);

                if (originalNode != null && resultantNode != null)
                {
                    contentChanges.Add(new ValidationContentChangeInfo
                    {
                        OriginalValidationExecutionStepContentNodeResultId = contentNodeResult.Id,
                        OriginalContentNodeId = contentNodeResult.OriginalContentNodeId,
                        ResultantContentNodeId = contentNodeResult.ResultantContentNodeId,
                        ApplicationStatus = contentNodeResult.ApplicationStatus,
                        OriginalText = originalNode.Text,
                        SuggestedText = resultantNode.Text,
                        ParentContentNodeId = originalNode.ParentId
                    });
                }
            }
        }

        if (!contentChanges.Any())
        {
            return NotFound($"No content changes found in validation results for document {documentId}");
        }

        var results = new ValidationResultsInfo
        {
            ValidationExecutionId = latestValidation.Id,
            ApplicationStatus = latestValidation.ApplicationStatus,
            ContentChanges = contentChanges,
            CompletedAt = latestValidation.ModifiedUtc
        };

        return Ok(results);
    }

    /// <summary>
    /// Updates the application status of a validation execution.
    /// </summary>
    /// <param name="validationExecutionId">The ID of the validation execution</param>
    /// <param name="status">The new application status</param>
    /// <returns>
    /// Produces Status Codes:
    ///     200 OK: When the status is updated successfully
    ///     404 NotFound: When the validation execution is not found
    /// </returns>
    [HttpPut("{validationExecutionId}/application-status/{status}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> UpdateValidationApplicationStatus(
        Guid validationExecutionId,
        ValidationPipelineExecutionApplicationStatus status)
    {
        var validationExecution = await _dbContext.ValidationPipelineExecutions
            .Include(x => x.ExecutionSteps)
            .ThenInclude(x => x.ValidationExecutionStepContentNodeResults)
            .FirstOrDefaultAsync(v => v.Id == validationExecutionId);

        if (validationExecution == null)
        {
            return NotFound($"Validation execution {validationExecutionId} not found");
        }

        // Update the application status for the validation execution and its steps
        validationExecution.ApplicationStatus = status;
        foreach (var step in validationExecution.ExecutionSteps)
        {
            step.ApplicationStatus = status;
            await UpdateContentNodeResultsAsync(step.ValidationExecutionStepContentNodeResults, status);
        }

        // Save changes if there are any
        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>
    /// Updates the content node results based on the new application status.
    /// </summary>
    /// <param name="contentNodeResults">The list of content node results to update.</param>
    /// <param name="status">The new application status.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpdateContentNodeResultsAsync(
        IEnumerable<ValidationExecutionStepContentNodeResult> contentNodeResults,
        ValidationPipelineExecutionApplicationStatus status)
    {
        var unappliedResults = contentNodeResults
            .Where(c => c.ApplicationStatus == ValidationContentNodeApplicationStatus.Unapplied)
            .ToList();

        foreach (var contentNodeResult in unappliedResults)
        {
            if (status == ValidationPipelineExecutionApplicationStatus.Applied)
            {
                contentNodeResult.ApplicationStatus = ValidationContentNodeApplicationStatus.Accepted;

                var newContentNode = await _dbContext.ContentNodes
                    .FirstOrDefaultAsync(c => c.Id == contentNodeResult.ResultantContentNodeId);

                if (newContentNode != null)
                {
                    await _contentNodeService.ReplaceContentNodeTextAsync(
                        contentNodeResult.OriginalContentNodeId,
                        newContentNode.Text,
                        ContentNodeVersioningReason.ValidationRun,
                        "Applied as part of accepting full validation execution step",
                        saveChanges: false);
                }
            }
            else if (status == ValidationPipelineExecutionApplicationStatus.Abandoned)
            {
                contentNodeResult.ApplicationStatus = ValidationContentNodeApplicationStatus.Rejected;
            }
        }
    }


    /// <summary>
    /// Updates the application status of a specific validation content change.
    /// </summary>
    /// <param name="contentChangeId">The ID of the validation content change.</param>
    /// <param name="status">The new application status.</param>
    /// <returns>
    /// Produces Status Codes:
    ///     200 OK: When the status is updated successfully.
    ///     404 NotFound: When the validation content change is not found.
    /// </returns>
    [HttpPut("content-change/{contentChangeId}/application-status/{status}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> UpdateValidationContentChangeStatus(
        Guid contentChangeId,
        ValidationContentNodeApplicationStatus status)
    {
        var contentChange = await _dbContext.ValidationExecutionStepContentNodeResults
            .FirstOrDefaultAsync(c => c.Id == contentChangeId);

        if (contentChange == null)
        {
            return NotFound($"Validation content change {contentChangeId} not found");
        }

        contentChange.ApplicationStatus = status;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }
}