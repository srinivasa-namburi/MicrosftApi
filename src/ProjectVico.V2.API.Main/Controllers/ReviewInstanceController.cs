using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.Review;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/review-instance")]
public class ReviewInstanceController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly AzureFileHelper _fileHelper;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public ReviewInstanceController(
        DocGenerationDbContext dbContext,
        AzureFileHelper fileHelper,
        IMapper mapper,
        IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _fileHelper = fileHelper;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

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

    [HttpGet("{reviewInstanceId:guid}/answers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<ReviewQuestionAnswerInfo>>]
    public async Task<ActionResult<List<ReviewQuestionAnswerInfo>>> GetReviewInstanceAnswers(Guid reviewInstanceId)
    {
        var reviewInstance = await _dbContext.ReviewInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(x=>x.Id == reviewInstanceId);

        if (reviewInstance == null)
        {
            return NotFound();
        }

        var reviewInstanceAnswers = await _dbContext.ReviewQuestionAnswers
            .Include(x=>x.OriginalReviewQuestion)
            .Where(x => x.ReviewInstanceId == reviewInstanceId)
            .AsNoTracking()
            .ToListAsync();

        var reviewQuestionAnswerList = _mapper.Map<List<ReviewQuestionAnswerInfo>>(reviewInstanceAnswers);
        return Ok(reviewQuestionAnswerList);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces(typeof(ActionResult<ReviewInstanceInfo>))]
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

    [HttpPost("{reviewInstanceId:guid}/execute")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    
    public async Task<ActionResult<ReviewInstanceInfo>> SubmitExecutionRequestForReviewInstance(Guid reviewInstanceId)
    {
        var reviewInstance = await _dbContext.ReviewInstances.FindAsync(reviewInstanceId);
        if (reviewInstance == null)
        {
            return NotFound();
        }

        // TODO : Validation of the review instance state before submitting the execution request

        await _publishEndpoint.Publish(new ExecuteReviewInstance(CorrelationId: reviewInstance.Id));
        var reviewInstanceInfo = _mapper.Map<ReviewInstanceInfo>(reviewInstance);

        // Submit the execution request

        return Accepted($"/api/review-instance/{reviewInstanceId}", reviewInstanceInfo);
    }
}