using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models.DocumentProcess;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/document-outline")]
[Route("/api/document-outlines")]
public class DocumentOutlineController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;

    public DocumentOutlineController(
        DocGenerationDbContext dbContext,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentOutlineInfo>>]
    public async Task<ActionResult<List<DocumentOutlineInfo>>> GetAllDocumentOutlines()
    {
        var documentOutlines = await _dbContext.DocumentOutlines.ToListAsync();
        if (documentOutlines.Count < 1)
        {
            return NotFound();
        }

        var outlines = _mapper.Map<List<DocumentOutlineInfo>>(documentOutlines);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DocumentOutlineInfo>]
    public async Task<ActionResult<DocumentOutlineInfo>> GetDocumentOutlineById(Guid id)
    {
        var documentOutline = await _dbContext.DocumentOutlines
            .Include(o => o.OutlineItems)
                .ThenInclude(a => a.Children)
                    .ThenInclude(b => b.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(d => d.Children)
                                .ThenInclude(e => e.Children)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (documentOutline == null)
        {
            return NotFound();
        }

        var outline = _mapper.Map<DocumentOutlineInfo>(documentOutline);
        return Ok(outline);
    }

    [HttpPost("{id:guid}/changes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<DocumentOutlineInfo>]
    public async Task<ActionResult<DocumentOutlineInfo>> UpdateDocumentOutline(Guid id, DocumentOutlineChangeRequest changeRequest)
    {
        var existingDocumentOutline = await _dbContext.DocumentOutlines.FindAsync(id);

        if (existingDocumentOutline == null)
        {
            return NotFound();
        }

        if (changeRequest.DocumentOutlineInfo != null)
        {
            _dbContext.Entry(existingDocumentOutline).CurrentValues.SetValues(changeRequest.DocumentOutlineInfo);
            _dbContext.Entry(existingDocumentOutline).State = EntityState.Modified;
            _dbContext.Update(existingDocumentOutline);
        }

        if (changeRequest.ChangedOutlineItems != null)
        {
            // Update the nested items
            foreach (var changedItem in changeRequest.ChangedOutlineItems)
            {
                if (changedItem.Id == Guid.Empty || changedItem.Id == null)
                {
                    // New item because there is no ID
                    var newItemDbModel = _mapper.Map<DocumentOutlineItem>(changedItem);
                    _dbContext.Entry(newItemDbModel).State = EntityState.Added;
                    await _dbContext.DocumentOutlineItems.AddAsync(newItemDbModel);
                }
                else
                {
                    // Existing item
                    var existingItem = await _dbContext.DocumentOutlineItems.FindAsync(changedItem.Id);
                    if (existingItem != null)
                    {
                        // We found an existing item with the ID set
                        var changedItemDbModel = _mapper.Map<DocumentOutlineItem>(changedItem);
                        _dbContext.Entry(existingItem).CurrentValues.SetValues(changedItemDbModel);
                        _dbContext.Entry(existingItem).State = EntityState.Modified;
                        _dbContext.Update(existingItem);
                    }
                    else
                    {
                        // We may also have a new item with the ID set
                        var newItemDbModel = _mapper.Map<DocumentOutlineItem>(changedItem);
                        _dbContext.Entry(newItemDbModel).State = EntityState.Added;
                        await _dbContext.DocumentOutlineItems.AddAsync(newItemDbModel);

                    }
                }

            }
        }

        if (changeRequest.DeletedOutlineItems != null)
        {
            // Delete the nested items
            foreach (var deletedItem in changeRequest.DeletedOutlineItems)
            {
                var existingItem = await _dbContext.DocumentOutlineItems.FindAsync(deletedItem.Id);
                if (existingItem != null)
                {
                    _dbContext.Entry(existingItem).State = EntityState.Deleted;
                    _dbContext.DocumentOutlineItems.Remove(existingItem);
                }
            }
        }

        // If DB Context has changes, save them
        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync();
        }

        // Reload the document outline from DB with all nested DocumentOutlineItems and their children
        var updatedDocumentOutline = await _dbContext.DocumentOutlines
            .Include(o => o.OutlineItems.OrderBy(i => i.OrderIndex))
                .ThenInclude(a => a.Children.OrderBy(i => i.OrderIndex))
                    .ThenInclude(b => b.Children.OrderBy(i => i.OrderIndex))
                        .ThenInclude(c => c.Children.OrderBy(i => i.OrderIndex))
                            .ThenInclude(d => d.Children.OrderBy(i => i.OrderIndex))
                                .ThenInclude(e => e.Children.OrderBy(i => i.OrderIndex))
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        var outline = _mapper.Map<DocumentOutlineInfo>(updatedDocumentOutline);
        return Ok(outline);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<DocumentOutlineInfo>> CreateDocumentOutline(DocumentOutlineInfo documentOutline)
    {
        var newDocumentOutline = _mapper.Map<DocumentOutline>(documentOutline);
        await _dbContext.DocumentOutlines.AddAsync(newDocumentOutline);
        await _dbContext.SaveChangesAsync();

        return Created("/api/document-outline/{newDocumentOutline.Id}", newDocumentOutline);
    }
}