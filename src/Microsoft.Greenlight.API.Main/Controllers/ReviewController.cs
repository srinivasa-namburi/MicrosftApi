using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing reviews.
/// </summary>
[Route("/api/review")]
public class ReviewController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    public ReviewController(
        DocGenerationDbContext dbContext,
        IMapper mapper
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets the list of reviews.
    /// </summary>
    /// <returns>A list of <see cref="ReviewDefinitionInfo"/>.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<ActionResult<List<ReviewDefinitionInfo>>>]
    public async Task<ActionResult<List<ReviewDefinitionInfo>>> GetReviews()
    {
        var reviews = await _dbContext.ReviewDefinitions.AsNoTracking().ToListAsync();
        var reviewInfos = _mapper.Map<List<ReviewDefinitionInfo>>(reviews);

        return Ok(reviewInfos);
    }

    /// <summary>
    /// Gets a review by its identifier.
    /// </summary>
    /// <param name="id">The review identifier.</param>
    /// <returns>A <see cref="ReviewDefinitionInfo"/>.
    /// Produces Status Codes:
    ///     200 Ok: When completed sucessfully
    ///     404 Not Found: When the review could not be found using the Id provided
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ActionResult<ReviewDefinitionInfo>>]
    public async Task<ActionResult<ReviewDefinitionInfo>> GetReviewById(Guid id)
    {
        var review = await _dbContext.ReviewDefinitions
            .Include(x => x.ReviewQuestions.OrderBy(q => q.Order))
            .Include(x => x.DocumentProcessDefinitionConnections)
                .ThenInclude(x => x.DocumentProcessDefinition)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (review == null)
        {
            return NotFound();
        }

        var reviewInfo = _mapper.Map<ReviewDefinitionInfo>(review);

        // Map document processes
        if (review.DocumentProcessDefinitionConnections != null)
        {
            reviewInfo.DocumentProcesses = review.DocumentProcessDefinitionConnections
                .Select(connection => new ReviewDefinitionDocumentProcessInfo
                {
                    Id = connection.Id,
                    ReviewDefinitionId = connection.ReviewId,
                    DocumentProcessDefinitionId = connection.DocumentProcessDefinitionId,
                    DocumentProcess = _mapper.Map<DocumentProcessInfo>(connection.DocumentProcessDefinition),
                    IsActive = connection.IsActive
                })
                .ToList();
        }
        
        // Ensure questions are ordered
        reviewInfo.ReviewQuestions = reviewInfo.ReviewQuestions.OrderBy(q => q.Order).ToList();

        return Ok(reviewInfo);
    }

    /// <summary>
    /// Creates a new review.
    /// </summary>
    /// <param name="reviewInfo">The review information.</param>
    /// <returns>The created <see cref="ReviewDefinitionInfo"/>.
    /// Produces Status Codes:
    ///     201 Created: When completed sucessfully
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<ReviewDefinitionInfo>]
    public async Task<ActionResult<ReviewDefinitionInfo>> CreateReview([FromBody] ReviewDefinitionInfo reviewInfo)
    {
        var review = _mapper.Map<ReviewDefinition>(reviewInfo);
        await _dbContext.ReviewDefinitions.AddAsync(review);
        await _dbContext.SaveChangesAsync();

        reviewInfo = _mapper.Map<ReviewDefinitionInfo>(review);
        return Created($"/api/review/{reviewInfo.Id}", reviewInfo);
    }

    /// <summary>
    /// Updates an existing review.
    /// </summary>
    /// <param name="id">The review identifier.</param>
    /// <param name="changeRequest">The change request containing updated review information.</param>
    /// <returns>The updated <see cref="ReviewDefinitionInfo"/>.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<ReviewDefinitionInfo>]
    public async Task<ActionResult<ReviewDefinitionInfo>> UpdateReview(Guid id, [FromBody] ReviewChangeRequest changeRequest)
    {
        var existingReview = await _dbContext.ReviewDefinitions
            .IgnoreAutoIncludes()
            .FirstOrDefaultAsync(x => x.Id == id);

        var originalReviewQuestions = _dbContext.ReviewQuestions.Where(x => x.ReviewId == id);
        var originalDocumentProcesses = _dbContext.Set<ReviewDefinitionDocumentProcessDefinition>().Where(x => x.ReviewId == id);

        if (changeRequest.ReviewDefinition != null)
        {
            _dbContext.Entry(existingReview!).CurrentValues.SetValues(changeRequest.ReviewDefinition);
            _dbContext.Entry(existingReview!).State = EntityState.Modified;
            _dbContext.Update(existingReview!);
        }

        // Calculate the next order value for new questions
        int nextOrderValue = await originalReviewQuestions.MaxAsync(q => (int?)q.Order) ?? 0;
        nextOrderValue++;

        // --- Process deletions first ---
        if (changeRequest.DeletedQuestions.Count > 0)
        {
            var deletedIds = changeRequest.DeletedQuestions.Select(q => q.Id).ToHashSet();
            var questionsToRemove = await _dbContext.ReviewQuestions
                .Where(x => x.ReviewId == id && deletedIds.Contains(x.Id))
                .ToListAsync();
            foreach (var questionToRemove in questionsToRemove)
            {
                _dbContext.Entry(questionToRemove).State = EntityState.Deleted;
                _dbContext.ReviewQuestions.Remove(questionToRemove);
            }
            await _dbContext.SaveChangesAsync(); // Commit deletions before proceeding
        }

        // --- Now process adds/updates, but skip any that were deleted ---
        var deletedQuestionIds = changeRequest.DeletedQuestions.Select(q => q.Id).ToHashSet();
        if (changeRequest.ChangedOrAddedQuestions.Count > 0)
        {
            foreach (var questionInfo in changeRequest.ChangedOrAddedQuestions)
            {
                if (deletedQuestionIds.Contains(questionInfo.Id))
                    continue; // Don't re-add a deleted question
                if (questionInfo.Id == Guid.Empty)
                {
                    // New Question because there is no ID
                    var questionModel = _mapper.Map<ReviewQuestion>(questionInfo);
                    
                    // If no order is specified, assign the next available order
                    if (questionModel.Order <= 0)
                    {
                        questionModel.Order = nextOrderValue++;
                    }
                    
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
                        
                        // Preserve order if not explicitly changed
                        if (questionModel.Order <= 0 && existingQuestion.Order > 0)
                        {
                            questionModel.Order = existingQuestion.Order;
                        }
                        else if (questionModel.Order <= 0)
                        {
                            questionModel.Order = nextOrderValue++;
                        }
                        
                        _dbContext.Entry(existingQuestion).CurrentValues.SetValues(questionModel);
                        _dbContext.Entry(existingQuestion).State = EntityState.Modified;
                        _dbContext.Update(existingQuestion);
                    }
                    else
                    {
                        // This is for the case when we have a new question with an ID
                        var questionModel = _mapper.Map<ReviewQuestion>(questionInfo);
                        
                        // Assign order if not specified
                        if (questionModel.Order <= 0)
                        {
                            questionModel.Order = nextOrderValue++;
                        }
                        
                        _dbContext.Entry(questionModel).State = EntityState.Added;
                        await _dbContext.ReviewQuestions.AddAsync(questionModel);
                    }
                }
            }
        }

        // --- Reorder remaining questions after deletions/adds/updates ---
        var remainingQuestions = await _dbContext.ReviewQuestions
            .Where(q => q.ReviewId == id)
            .ToListAsync();
        if (remainingQuestions.All(q => q.Order == 0))
        {
            remainingQuestions = remainingQuestions.OrderBy(q => q.CreatedUtc).ToList();
        }
        else
        {
            remainingQuestions = remainingQuestions.OrderBy(q => q.Order).ToList();
        }
        for (int i = 0; i < remainingQuestions.Count; i++)
        {
            remainingQuestions[i].Order = i + 1;
            _dbContext.Entry(remainingQuestions[i]).State = EntityState.Modified;
        }

        // Handle document process changes
        if (changeRequest.ChangedOrAddedDocumentProcesses.Count > 0)
        {
            foreach (var processInfo in changeRequest.ChangedOrAddedDocumentProcesses)
            {
                if (processInfo.Id == Guid.Empty)
                {
                    // New document process association
                    var processModel = new ReviewDefinitionDocumentProcessDefinition
                    {
                        ReviewId = id,
                        DocumentProcessDefinitionId = processInfo.DocumentProcessDefinitionId,
                        IsActive = processInfo.IsActive
                    };

                    _dbContext.Entry(processModel).State = EntityState.Added;
                    await _dbContext.Set<ReviewDefinitionDocumentProcessDefinition>().AddAsync(processModel);
                }
                else
                {
                    // Existing document process association with updates
                    var existingProcess = await originalDocumentProcesses.FirstOrDefaultAsync(x => x.Id == processInfo.Id);
                    if (existingProcess != null)
                    {
                        existingProcess.IsActive = processInfo.IsActive;
                        _dbContext.Entry(existingProcess).State = EntityState.Modified;
                        _dbContext.Update(existingProcess);
                    }
                }
            }
        }

        if (changeRequest.DeletedDocumentProcesses.Count > 0)
        {
            foreach (var processInfo in changeRequest.DeletedDocumentProcesses)
            {
                var processToRemove = await originalDocumentProcesses.FirstOrDefaultAsync(x => x.Id == processInfo.Id);
                if (processToRemove != null)
                {
                    _dbContext.Entry(processToRemove).State = EntityState.Deleted;
                    _dbContext.Set<ReviewDefinitionDocumentProcessDefinition>().Remove(processToRemove);
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        // reload with changes
        existingReview = _dbContext.ReviewDefinitions
            .Include(x => x.ReviewQuestions.OrderBy(q => q.Order))
            .Include(x => x.DocumentProcessDefinitionConnections)
                .ThenInclude(x => x.DocumentProcessDefinition)
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == id);

        var reviewInfo = _mapper.Map<ReviewDefinitionInfo>(existingReview);

        // Map document processes
        if (existingReview?.DocumentProcessDefinitionConnections != null)
        {
            reviewInfo.DocumentProcesses = existingReview.DocumentProcessDefinitionConnections
                .Select(connection => new ReviewDefinitionDocumentProcessInfo
                {
                    Id = connection.Id,
                    ReviewDefinitionId = connection.ReviewId,
                    DocumentProcessDefinitionId = connection.DocumentProcessDefinitionId,
                    DocumentProcess = _mapper.Map<DocumentProcessInfo>(connection.DocumentProcessDefinition),
                    IsActive = connection.IsActive
                })
                .ToList();
        }
        
        // Ensure questions are ordered
        reviewInfo.ReviewQuestions = reviewInfo.ReviewQuestions.OrderBy(q => q.Order).ToList();

        return Ok(reviewInfo);
    }

    /// <summary>
    /// Deletes a review by its identifier.
    /// </summary>
    /// <param name="id">The review identifier.</param>
    /// <returns>An <see cref="IActionResult"/> indicating the result of the operation.
    /// Produces Status Codes:
    ///     204 No Content: When completed sucessfully
    ///     404 Not Found: When the review could not be found using the id provided
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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

        return NoContent();
    }

    /// <summary>
    /// Gets recent review instances.
    /// </summary>
    /// <param name="count">Optional. Number of recent instances to return. If 0 or not specified, returns all instances.</param>
    /// <returns>A list of recent <see cref="ReviewInstanceInfo"/> ordered by creation date.
    /// Produces Status Codes:
    ///     200 Ok: When completed successfully
    ///     404 Not Found: When no review instances could be found
    /// </returns>
    [HttpGet("recent-instances")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<ReviewInstanceInfo>>]
    public async Task<ActionResult<List<ReviewInstanceInfo>>> GetRecentReviewInstances([FromQuery] int count = 0)
    {
        IQueryable<ReviewInstance> query = _dbContext.ReviewInstances
            .Include(ri => ri.ReviewDefinition)
            .OrderByDescending(ri => ri.CreatedUtc);

        if (count > 0)
        {
            query = query.Take(count);
        }

        var reviewInstances = await query.ToListAsync();

        if (reviewInstances.Count < 1)
        {
            return NotFound();
        }

        var reviewInstanceDtos = _mapper.Map<List<ReviewInstanceInfo>>(reviewInstances);
        return Ok(reviewInstanceDtos);
    }

    /// <summary>
    /// Gets document processes associated with a review definition.
    /// </summary>
    /// <param name="id">The review definition identifier.</param>
    /// <returns>A list of <see cref="DocumentProcessInfo"/> associated with the review definition.
    /// Produces Status Codes:
    ///     200 Ok: When completed successfully
    ///     404 Not Found: When the review definition could not be found using the id provided
    /// </returns>
    [HttpGet("{id:guid}/document-processes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessInfo>>]
    public async Task<ActionResult<List<DocumentProcessInfo>>> GetDocumentProcessesByReviewDefinitionId(Guid id)
    {
        var reviewDefinition = await _dbContext.ReviewDefinitions
            .Include(r => r.DocumentProcessDefinitionConnections)
                .ThenInclude(c => c.DocumentProcessDefinition)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reviewDefinition == null)
        {
            return NotFound();
        }

        var documentProcesses = reviewDefinition.DocumentProcessDefinitionConnections
            .Where(c => c.IsActive)
            .Select(c => c.DocumentProcessDefinition)
            .Where(dp => dp != null)
            .ToList();

        var documentProcessInfos = _mapper.Map<List<DocumentProcessInfo>>(documentProcesses);
        return Ok(documentProcessInfos);
    }
}

