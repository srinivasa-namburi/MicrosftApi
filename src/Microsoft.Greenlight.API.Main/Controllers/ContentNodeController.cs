using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing content nodes.
/// </summary>
[Route("/api/content-nodes")]
public class ContentNodeController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IContentNodeService _contentNodeService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentNodeController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="contentNodeService">Content Node service</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    public ContentNodeController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IContentNodeService contentNodeService
    )
    {
        _dbContext = dbContext;
        _contentNodeService = contentNodeService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a content node by its ID.
    /// </summary>
    /// <param name="contentNodeId">The ID of the content node.</param>
    /// <returns>The content node information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When the content node with a cooresponding Content Node Id is not found
    /// </returns>
    [HttpGet("{contentNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ContentNodeInfo>]
    public async Task<ActionResult<ContentNodeInfo>> GetContentNode(string contentNodeId)
    {
        var contentNodeGuid = Guid.Parse(contentNodeId);

        // Step 1: Load the initial ContentNode
        var initialContentNode = await _dbContext.ContentNodes
            .Include(cn => cn.ContentNodeSystemItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(cn => cn.Id == contentNodeGuid);

        if (initialContentNode == null)
        {
            return NotFound();
        }

        // Step 2: Load all descendants
        var allContentNodes = new List<ContentNode> { initialContentNode };

        // Use a queue to iteratively load descendants
        var queue = new Queue<ContentNode>();
        queue.Enqueue(initialContentNode);

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();

            // Load children of the current parent
            var children = await _dbContext.ContentNodes
                .Include(cn => cn.ContentNodeSystemItem)
                .Where(cn => cn.ParentId == parent.Id)
                .AsNoTracking()
                .ToListAsync();

            // Assign children to the parent
            parent.Children = children;

            // Add children to the list and enqueue them for further processing
            foreach (var child in children)
            {
                allContentNodes.Add(child);
                queue.Enqueue(child);
            }
        }

        // Step 3: Map the ContentNode to ContentNodeInfo
        var contentNodeInfo = _mapper.Map<ContentNodeInfo>(initialContentNode);

        return Ok(contentNodeInfo);
    }

    /// <summary>
    /// Gets a content node system item by its ID.
    /// </summary>
    /// <param name="contentNodeSystemItemId">The ID of the content node system item.</param>
    /// <returns>The content node system item information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When the content node system item with a cooresponding Content Node System Item 
    ///     Id is not found
    /// </returns>
    [HttpGet("content-node-system-item/{contentNodeSystemItemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ContentNodeSystemItemInfo>]
    public async Task<ActionResult<ContentNodeSystemItemInfo>> GetContentNodeSystemItem(Guid contentNodeSystemItemId)
    {
        var contentNodeSystemItem = await _dbContext.ContentNodeSystemItems.Where(d => d.Id == contentNodeSystemItemId)
            .Include(x => x.SourceReferences)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (contentNodeSystemItem == null)
        {
            return NotFound();
        }

        var contentNodeSystemItemInfo = _mapper.Map<ContentNodeSystemItemInfo>(contentNodeSystemItem);

        return Ok(contentNodeSystemItemInfo);
    }

    /// <summary>
    /// Gets all versions of a content node.
    /// </summary>
    /// <param name="contentNodeId">The ID of the content node.</param>
    /// <returns>List of content node versions.</returns>
    [HttpGet("{contentNodeId:guid}/versions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<ContentNodeVersion>>]
    public async Task<ActionResult<List<ContentNodeVersion>>> GetContentNodeVersions(Guid contentNodeId)
    {
        var versions = await _contentNodeService.GetContentNodeVersionsAsync(contentNodeId);
        return Ok(versions);
    }

    /// <summary>
    /// Checks if a content node has previous versions.
    /// </summary>
    /// <param name="contentNodeId">The ID of the content node.</param>
    /// <returns>True if the content node has previous versions, false otherwise.</returns>
    [HttpGet("{contentNodeId:guid}/has-versions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<bool>> HasContentNodeVersions(Guid contentNodeId)
    {
        var versions = await _contentNodeService.GetContentNodeVersionsAsync(contentNodeId);
        return Ok(versions.Count > 0);
    }

    /// <summary>
    /// Updates the text of a content node.
    /// </summary>
    /// <param name="contentNodeId">The ID of the content node.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated content node.</returns>
    [HttpPut("{contentNodeId:guid}/text")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<ContentNodeInfo>]
    public async Task<ActionResult<ContentNodeInfo>> UpdateContentNodeText(Guid contentNodeId, [FromBody] UpdateContentNodeTextRequest request)
    {
        var updatedNode = await _contentNodeService.ReplaceContentNodeTextAsync(
            contentNodeId, request.NewText, 
            request.VersioningReason, 
            request.Comment,
            saveChanges:true);

        if (updatedNode == null)
        {
            return BadRequest("Content node could not be updated. Only BodyText nodes can be updated.");
        }

        var contentNodeInfo = _mapper.Map<ContentNodeInfo>(updatedNode);
        return Ok(contentNodeInfo);
    }

    /// <summary>
    /// Promotes a previous version of a content node.
    /// </summary>
    /// <param name="contentNodeId">The ID of the content node.</param>
    /// <param name="request">The promote version request.</param>
    /// <returns>The updated content node.</returns>
    [HttpPut("{contentNodeId:guid}/promote-version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<ContentNodeInfo>]
    public async Task<ActionResult<ContentNodeInfo>> PromoteContentNodeVersion(Guid contentNodeId, [FromBody] PromoteContentNodeVersionRequest request)
    {
        var updatedNode = await _contentNodeService.PromotePreviousVersionAsync(contentNodeId, request.VersionId, request.Comment);

        if (updatedNode == null)
        {
            return BadRequest("Version promotion failed. Only BodyText nodes can have versions promoted.");
        }

        var contentNodeInfo = _mapper.Map<ContentNodeInfo>(updatedNode);
        return Ok(contentNodeInfo);
    }
}
