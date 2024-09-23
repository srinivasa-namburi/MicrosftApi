using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/review")]
public class ReviewController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly IMapper _mapper;


    public ReviewController(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        AzureFileHelper fileHelper,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(ActionResult<List<ReviewDefinitionInfo>>))]
    public async Task<ActionResult<List<ReviewDefinitionInfo>>> GetReviews()
    {
        var reviews = await _dbContext.ReviewDefinitions.AsNoTracking().ToListAsync();
        var reviewInfos = _mapper.Map<List<ReviewDefinitionInfo>>(reviews);

        return Ok(reviewInfos);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(ActionResult<ReviewDefinitionInfo>))]
    public async Task<ActionResult<ReviewDefinitionInfo>> GetReviewById(Guid id)
    {
        var review = await _dbContext.ReviewDefinitions
            .Include(x => x.ReviewQuestions)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (review == null)
        {
            return NotFound();
        }

        var reviewInfo = _mapper.Map<ReviewDefinitionInfo>(review);

        return Ok(reviewInfo);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces(typeof(ReviewDefinitionInfo))]
    public async Task<ActionResult<ReviewDefinitionInfo>> CreateReview([FromBody] ReviewDefinitionInfo reviewInfo)
    {
        var review = _mapper.Map<ReviewDefinition>(reviewInfo);
        await _dbContext.ReviewDefinitions.AddAsync(review);
        await _dbContext.SaveChangesAsync();

        reviewInfo = _mapper.Map<ReviewDefinitionInfo>(review);
        return Created($"/api/review/{reviewInfo.Id}", reviewInfo);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Consumes("application/json")]
    public async Task<ActionResult<ReviewDefinitionInfo>> UpdateReview(Guid id, [FromBody] ReviewChangeRequest changeRequest)
    {
        var existingReview = await _dbContext.ReviewDefinitions
            .IgnoreAutoIncludes()
            .FirstOrDefaultAsync(x => x.Id == id);

        var originalReviewQuestions = _dbContext.ReviewQuestions.Where(x => x.ReviewId == id);

        if (changeRequest.ReviewDefinition != null)
        {
            _dbContext.Entry(existingReview).CurrentValues.SetValues(changeRequest.ReviewDefinition);
            _dbContext.Entry(existingReview).State = EntityState.Modified;
            _dbContext.Update(existingReview);
        }

        if (changeRequest.ChangedOrAddedQuestions.Count > 0)
        {
            foreach (var questionInfo in changeRequest.ChangedOrAddedQuestions)
            {

                if (questionInfo.Id == Guid.Empty)
                {
                    // New Question because there is no ID
                    var questionModel = _mapper.Map<ReviewQuestion>(questionInfo);
                    _dbContext.Entry(questionModel).State = EntityState.Added;
                    await _dbContext.ReviewQuestions.AddAsync(questionModel);
                }
                else
                {
                    // Existing Question with updates
                    var existingQuestion = await originalReviewQuestions.FirstOrDefaultAsync(x => x.Id == questionInfo.Id);
                    if (existingQuestion != null)
                    {
                        var questionModel = _mapper.Map<ReviewQuestion>(questionInfo);
                        _dbContext.Entry(existingQuestion).CurrentValues.SetValues(questionModel);
                        _dbContext.Entry(existingQuestion).State = EntityState.Modified;
                        _dbContext.Update(existingQuestion);
                    }
                    else
                    {
                        // This is for the case when we have a new question with an ID
                        // We only come here after checking if the question with the ID already exists
                        var questionModel = _mapper.Map<ReviewQuestion>(questionInfo);
                        _dbContext.Entry(questionModel).State = EntityState.Added;
                        await _dbContext.ReviewQuestions.AddAsync(questionModel);
                    }
                }
            }
        }

        if (changeRequest.DeletedQuestions.Count > 0)
        {
            var mappedDeletedQuestions = _mapper.Map<List<ReviewQuestion>>(changeRequest.DeletedQuestions);

            foreach (var question in mappedDeletedQuestions)
            {
                var questionToRemove = await originalReviewQuestions.FirstOrDefaultAsync(x => x.Id == question.Id);
                if (questionToRemove != null)
                {
                    _dbContext.Entry(questionToRemove).State = EntityState.Deleted;
                    _dbContext.ReviewQuestions.Remove(questionToRemove);
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        // reload with changes
        existingReview = _dbContext.ReviewDefinitions.Include(x => x.ReviewQuestions)
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == id);

        var reviewInfo = _mapper.Map<ReviewDefinitionInfo>(existingReview);
        return Ok(reviewInfo);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var review = await _dbContext.ReviewDefinitions
            .Include(x => x.ReviewQuestions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (review == null)
        {
            return NotFound();
        }

        _dbContext.ReviewDefinitions.Remove(review);
        await _dbContext.SaveChangesAsync();

        return Accepted();
    }
}

