using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/content-nodes")]
public class ContentNodeController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;

    public ContentNodeController(
        DocGenerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{contentNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ContentNode>]
    public async Task<ActionResult<ContentNode>> GetContentNode(string contentNodeId)
    {
        var contentNodeGuid = Guid.Parse(contentNodeId);
        var contentNode = await _dbContext.ContentNodes
            .Include(w => w.Children)
                .ThenInclude(r=>r.Children)
                    .ThenInclude(s=>s.Children)
                        .ThenInclude(t=>t.Children)
                            .ThenInclude(u=>u.Children)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == contentNodeGuid);

        if (contentNode == null)
        {
            return NotFound();
        }

        return Ok(contentNode);
    }
   
}