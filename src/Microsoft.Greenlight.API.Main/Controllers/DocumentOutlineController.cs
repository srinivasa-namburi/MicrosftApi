using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document outlines.
/// </summary>
[Route("/api/document-outline")]
[Route("/api/document-outlines")]
public class DocumentOutlineController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOutlineController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    public DocumentOutlineController(
        DocGenerationDbContext dbContext,
        IMapper mapper
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all document outlines.
    /// </summary>
    /// <returns>A list of document outlines.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document outlines are available
    /// </returns>
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
        return Ok(outlines);
    }

    /// <summary>
    /// Gets a document outline by its ID.
    /// </summary>
    /// <param name="id">The ID of the document outline.</param>
    /// <returns>The document outline.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document outlines are found using the Id provided
    /// </returns>
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

    /// <summary>
    /// Updates a document outline.
    /// </summary>
    /// <param name="id">The ID of the document outline.</param>
    /// <param name="changeRequest">The change request containing the updated information.</param>
    /// <returns>The updated document outline.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document outlines are found using the Id provided
    /// </returns>
    [HttpPost("{id:guid}/changes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
                if (changedItem.Level == 0)
                {
                    changedItem.DocumentOutlineId = id;
                }
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

    /// <summary>
    /// Creates a new document outline.
    /// </summary>
    /// <param name="documentOutline">The document outline information.</param>
    /// <returns>The created document outline.
    /// Produces Status Codes:
    ///     201 Created: When the document outline was sucessfully created
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<DocumentOutlineInfo>]
    public async Task<ActionResult<DocumentOutlineInfo>> CreateDocumentOutline(DocumentOutlineInfo documentOutline)
    {
        var newDocumentOutline = _mapper.Map<DocumentOutline>(documentOutline);
        await _dbContext.DocumentOutlines.AddAsync(newDocumentOutline);
        await _dbContext.SaveChangesAsync();

        return Created($"/api/document-outline/{newDocumentOutline.Id}", newDocumentOutline);
    }

    /// <summary>
    /// Generates an outline from text.
    /// </summary>
    /// <param name="textDto">The text DTO containing the outline text.</param>
    /// <returns>A list of document outline items.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When there is no Text provided in the SimpleTextDTO object provided
    /// </returns>
    [HttpPost("generate-from-text")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<List<DocumentOutlineItemInfo>>]
    public ActionResult<List<DocumentOutlineItemInfo>> GenerateOutlineFromText(SimpleTextDTO textDto)
    {
        var outlineText = textDto.Text;

        if (string.IsNullOrWhiteSpace(outlineText))
        {
            return BadRequest("Outline text cannot be empty.");
        }

        // the outline text is a json string with \n for line feeds that need to be turned into a regular string with regular line feeds
        outlineText = outlineText.Replace("\\n", "\n");

        var documentOutlineLines = outlineText.Split("\n").ToList();

        // Foreach line in the lines List, remove quotes as well as leading and trailing whitespace
        documentOutlineLines = documentOutlineLines.Select(x => x.Trim([' ', '"', '-'])
                .Replace("[", "")
                .Replace("]", ""))
            .ToList();

        // Remove any empty lines
        documentOutlineLines = documentOutlineLines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        // Re-create the string with the outline text - each line separated by a newline
        outlineText = string.Join("\n", documentOutlineLines);

        // This renders the Outline Items based on the text
        var outline = new DocumentOutline
        {
            FullText = outlineText
        };

        var infoOutline = _mapper.Map<List<DocumentOutlineItemInfo>>(outline.OutlineItems);

        CleanSectionTitles(infoOutline);
        SetOrderAndLevelProperties(infoOutline);

        return Ok(infoOutline);
    }

    /// <summary>
    /// Sets the order and level properties for the outline items.
    /// </summary>
    /// <param name="infoOutline">The list of document outline items.</param>
    /// <param name="level">The current level.</param>
    /// <param name="order">The current order.</param>
    private static void SetOrderAndLevelProperties(List<DocumentOutlineItemInfo> infoOutline, int level = 0, int order = 0)
    {
        foreach (var item in infoOutline)
        {
            item.Level = level;
            item.OrderIndex = order;
            order += 1;

            if (item.Children.Count > 0)
            {
                SetOrderAndLevelProperties(item.Children, level + 1, 0);
            }
        }
    }

    /// <summary>
    /// Cleans the section titles of the outline items.
    /// </summary>
    /// <param name="items">The list of document outline items.</param>
    private static void CleanSectionTitles(List<DocumentOutlineItemInfo> items)
    {
        foreach (var item in items)
        {
            item.SectionTitle = item.SectionTitle.TrimStart(' ', '.');
            item.SectionNumber = item.SectionNumber?.TrimStart(' ', '.');

            if (item.Children.Count > 0)
            {
                CleanSectionTitles(item.Children);
            }
        }
    }
}
