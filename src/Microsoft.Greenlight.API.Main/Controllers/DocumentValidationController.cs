using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document validation operations.
/// </summary>
[Route("/api/document-validation")]
public class DocumentValidationController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DocumentValidationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentValidationController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for document generation.</param>
    /// <param name="publishEndpoint">The publish endpoint for sending messages.</param>
    /// <param name="logger">The logger for this controller.</param>
    public DocumentValidationController(
        DocGenerationDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<DocumentValidationController> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Starts the validation pipeline for a specific document by its ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to validate.</param>
    /// <returns>
    /// Produces Status Codes:
    ///     202 Accepted: When the validation request has been sent to the workers
    ///     404 NotFound: When the document with the specified ID doesn't exist
    /// </returns>
    [HttpPost("{documentId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> StartDocumentValidation(Guid documentId)
    {
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found when attempting to start validation", documentId);
            return NotFound($"Document with ID {documentId} not found");
        }

        // Check if the document has a document process
        if (string.IsNullOrEmpty(document.DocumentProcess))
        {
            _logger.LogWarning("Document {DocumentId} has no associated document process", documentId);
            return BadRequest("Document has no associated document process required for validation");
        }

        // Publish the validation command
        await _publishEndpoint.Publish(new ValidateGeneratedDocument(documentId));
        
        _logger.LogInformation("Started validation process for document {DocumentId}", documentId);
        return Accepted($"Validation process started for document {documentId}");
    }

    /// <summary>
    /// Gets the validation status for a specific document.
    /// </summary>
    /// <param name="documentId">The ID of the document to check validation status for.</param>
    /// <returns>
    /// The validation status information.
    /// Produces Status Codes:
    ///     200 OK: When the status is retrieved successfully
    ///     404 NotFound: When no validation data exists for the specified document ID
    /// </returns>
    [HttpGet("{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> GetDocumentValidationStatus(Guid documentId)
    {
        // First check if the document exists
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            return NotFound($"Document with ID {documentId} not found");
        }

        // Then look for any validation executions for this document
        var validationExecutions = await _dbContext.ValidationPipelineExecutions
            .AsNoTracking()
            .Include(x => x.ExecutionSteps)
            .Join(
                _dbContext.DocumentProcessValidationPipelines,
                vpe => vpe.DocumentProcessValidationPipelineId,
                dpvp => dpvp.Id,
                (vpe, dpvp) => new { Execution = vpe, DocumentProcessId = dpvp.DocumentProcessId }
            )
            .Join(
                _dbContext.DynamicDocumentProcessDefinitions,
                joined => joined.DocumentProcessId,
                ddpd => ddpd.Id,
                (joined, ddpd) => new { joined.Execution, DocumentProcess = ddpd }
            )
            .Where(x => x.DocumentProcess.ShortName == document.DocumentProcess)
            .Select(x => x.Execution)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync();

        if (!validationExecutions.Any())
        {
            return NotFound($"No validation executions found for document {documentId}");
        }

        // Get the most recent validation execution
        var latestValidation = validationExecutions.First();
        
        // Create a response with validation status details
        var response = new
        {
            DocumentId = documentId,
            ValidationId = latestValidation.Id,
            Started = latestValidation.CreatedUtc,
            Steps = latestValidation.ExecutionSteps.OrderBy(s => s.Order).Select(s => new
            {
                StepId = s.Id,
                Order = s.Order,
                ExecutionType = s.PipelineExecutionType.ToString(),
                Status = s.PipelineExecutionStepStatus.ToString()
            }).ToList()
        };

        return Ok(response);
    }
}