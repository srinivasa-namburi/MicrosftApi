using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing review instances.
/// </summary>
[Route("/api/review-instance")]
public class ReviewInstanceController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IReviewService _reviewService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewInstanceController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="reviewService">The review service.</param>
    public ReviewInstanceController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IReviewService reviewService
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _reviewService = reviewService;
    }

    /// <summary>
    /// Gets all review instances.
    /// </summary>
    /// <returns>A list of review instances.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    ///     404 Not Found: When no review instances could be found
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<ReviewInstanceInfo>>]
    public async Task<ActionResult<List<ReviewInstanceInfo>>> GetAllReviewInstances()
    {
        var reviewInstances = await _dbContext.ReviewInstances.ToListAsync();
        if (reviewInstances.Count < 1)
        {
            return NotFound();
        }

        var reviewInstanceDtos = _mapper.Map<List<ReviewInstanceInfo>>(reviewInstances);
        return Ok(reviewInstanceDtos);
    }

    /// <summary>
    /// Gets a review instance by its ID.
    /// </summary>
    /// <param name="reviewInstanceId">The review instance ID.</param>
    /// <returns>The review instance.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    ///     404 Not Found: When the review instance could not be found using the review instance id provided
    /// </returns>
    [HttpGet("{reviewInstanceId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ReviewInstanceInfo>]
    public async Task<ActionResult<ReviewInstanceInfo>> GetReviewInstanceById(Guid reviewInstanceId)
    {
        var reviewInstance = await _dbContext.ReviewInstances.FindAsync(reviewInstanceId);
        if (reviewInstance == null)
        {
            return NotFound();
        }

        var reviewInstanceDto = _mapper.Map<ReviewInstanceInfo>(reviewInstance);
        return Ok(reviewInstanceDto);
    }

    /// <summary>
    /// Gets the answers for a review instance.
    /// </summary>
    /// <param name="reviewInstanceId">The review instance ID.</param>
    /// <returns>A list of review question answers.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    ///     404 Not Found: When the review instance could not be found using the review instance id provided
    /// </returns>
    [HttpGet("{reviewInstanceId:guid}/answers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<ReviewQuestionAnswerInfo>>]
    public async Task<ActionResult<List<ReviewQuestionAnswerInfo>>> GetReviewInstanceAnswers(Guid reviewInstanceId)
    {
        var reviewInstance = await _dbContext.ReviewInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

        if (reviewInstance == null)
        {
            return NotFound();
        }

        var reviewInstanceAnswers = await _dbContext.ReviewQuestionAnswers
            .Include(x => x.OriginalReviewQuestion)
            .Where(x => x.ReviewInstanceId == reviewInstanceId)
            .AsNoTracking()
            .ToListAsync();

        var reviewQuestionAnswerList = _mapper.Map<List<ReviewQuestionAnswerInfo>>(reviewInstanceAnswers);
        return Ok(reviewQuestionAnswerList);
    }

    /// <summary>
    /// Creates a new review instance.
    /// </summary>
    /// <param name="reviewInstanceInfo">The review instance information.</param>
    /// <returns>The created review instance.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    ///     400 Bad Request: When the review defintion id on the review instance is not found,
    ///     or the export link id on the review instance is not found
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<ActionResult<ReviewInstanceInfo>>]
    public async Task<ActionResult<ReviewInstanceInfo>> CreateReviewInstance(
        [FromBody] ReviewInstanceInfo reviewInstanceInfo)
    {
        if (reviewInstanceInfo.Id == Guid.Empty)
        {
            reviewInstanceInfo.Id = Guid.NewGuid();
        }

        // validate that the review definition and exported link exist
        var reviewDefinition = await _dbContext.ReviewDefinitions.FindAsync(reviewInstanceInfo.ReviewDefinitionId);
        if (reviewDefinition == null)
        {
            return BadRequest("ReviewId is invalid");
        }

        var exportedDocumentLink = await _dbContext.ExportedDocumentLinks.FindAsync(reviewInstanceInfo.ExportedLinkId);
        if (exportedDocumentLink == null)
        {
            return BadRequest("ExportedLinkId is invalid");
        }

        // Map into a domain model
        var reviewInstanceModel = _mapper.Map<ReviewInstance>(reviewInstanceInfo);

        // Create the review instance
        var resultModel = await _dbContext.ReviewInstances.AddAsync(reviewInstanceModel);
        await _dbContext.SaveChangesAsync();

        var resultDto = _mapper.Map<ReviewInstanceInfo>(resultModel.Entity);

        // The ReviewDefinitionStateWhenSubmitted is generated during conversion to DTO, but we want to store it back in the database
        resultModel.Entity.ReviewDefinitionStateWhenSubmitted = resultDto.ReviewDefinitionStateWhenSubmitted;
        _dbContext.Update(resultModel.Entity);
        await _dbContext.SaveChangesAsync();
        return Ok(resultDto);
    }

    /// <summary>
    /// Submits an execution request for a review instance.
    /// </summary>
    /// <param name="reviewInstanceId">The review instance ID.</param>
    /// <returns>The review instance information.
    /// Produces Status Codes:
    ///     202 Accepted: When the execution request is submitted successfully
    ///     404 Not Found: When the review instance could not be found using the review instance id provided
    /// </returns>
    [HttpPost("{reviewInstanceId:guid}/execute")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewInstanceInfo>> SubmitExecutionRequestForReviewInstance(Guid reviewInstanceId)
    {
        var reviewInstance = await _dbContext.ReviewInstances.FindAsync(reviewInstanceId);
        if (reviewInstance == null)
        {
            return NotFound();
        }

        // Validate the review instance state before submitting the execution request
        if (reviewInstance.Status != ReviewInstanceStatus.Pending)
        {
            return BadRequest("Review instance is not in a valid state for execution.");
        }

        // Use the ReviewService to execute the review - this triggers execution in Orleans
        var executionSuccess = await _reviewService.ExecuteReviewAsync(reviewInstanceId);
        if (!executionSuccess)
        {
            return BadRequest("Failed to submit the execution request for the review instance.");
        }

        var reviewInstanceInfo = _mapper.Map<ReviewInstanceInfo>(reviewInstance);

        return Accepted($"/api/review-instance/{reviewInstanceId}", reviewInstanceInfo);
    }
}
