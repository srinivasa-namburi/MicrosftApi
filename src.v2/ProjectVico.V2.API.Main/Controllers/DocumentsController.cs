using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/documents")]
public class DocumentsController : BaseController
{
    
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DocGenerationDbContext _dbContext;

    public DocumentsController(
        IPublishEndpoint publishEndpoint,
        IHttpContextAccessor httpContextAccessor,
        DocGenerationDbContext dbContext)
    {
        _publishEndpoint = publishEndpoint;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateDocument([FromBody]GenerateDocumentDTO generateDocumentDto)
    {
        var claimsPrincipal = _httpContextAccessor.HttpContext.User;
        generateDocumentDto.AuthorOid = claimsPrincipal.GetObjectId();
        
        await _publishEndpoint.Publish<GenerateDocumentDTO>(generateDocumentDto);
        
        return Accepted();
    }

    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> IngestDocument([FromBody] DocumentIngestionRequest documentIngestionRequest)
    {
        var claimsPrincipal = _httpContextAccessor.HttpContext.User;

        if (documentIngestionRequest.Id == Guid.Empty)
        {
            documentIngestionRequest.Id = Guid.NewGuid();
        }

        documentIngestionRequest.UploadedByUserOid = claimsPrincipal.GetObjectId();
        await _publishEndpoint.Publish<DocumentIngestionRequest>(documentIngestionRequest);
        
        return Accepted();
    }

    [HttpPost("reindex-all")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReindexAllCompletedDocuments()
    {
        await _publishEndpoint.Publish<ReindexAllCompletedDocuments>(new ReindexAllCompletedDocuments(Guid.NewGuid()));
        return Accepted();
    }

    [HttpGet("{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<GeneratedDocument>]
    public async Task<ActionResult<GeneratedDocument>> GetDocument(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);
        var document = await _dbContext.GeneratedDocuments
            .Include(w => w.ContentNodes)
            .ThenInclude(r=>r.Children)
                .ThenInclude(s=>s.Children)
                    .ThenInclude(t=>t.Children)
                        .ThenInclude(u=>u.Children)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == documentGuid);
        
        if (document == null)
        {
            return NotFound();
        }

        return document;
    }

    [HttpDelete("{documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);
        var document = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(d => d.Id == documentGuid);
        
        if (document == null)
        {
            return NotFound();
        }

        // Interceptor isn't working, so we have to manually set these properties
        document.IsActive = false;
        document.DeletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<GeneratedDocumentListItem>>]
    public async Task<ActionResult<List<GeneratedDocumentListItem>>?> GetGeneratedDocuments()
    {
        var documents = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .Select(d => new GeneratedDocumentListItem
            {
                Id = d.Id,
                Title = d.Title,
                GeneratedDate = d.GeneratedDate,
                RequestingAuthorOid = d.RequestingAuthorOid
            })
            .ToListAsync();

        if (documents == null)
        {
            return NotFound();
        }

        return documents;
    }
}