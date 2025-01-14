using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing prompts.
/// </summary>
[Route("/api/prompts")]
public class PromptsController : BaseController
{
    private readonly IPromptInfoService _promptInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptsController"/> class.
    /// </summary>
    /// <param name="promptInfoService">The prompt info service.</param>
    public PromptsController(IPromptInfoService promptInfoService)
    {
        _promptInfoService = promptInfoService;
    }

    /// <summary>
    /// Gets the prompts by process identifier.
    /// </summary>
    /// <param name="processId">The process identifier.</param>
    /// <returns>A list of prompts. 
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When no prompts could be found using the process id provided
    /// </returns>
    [HttpGet("by-process/{processId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(List<PromptInfo>))]
    public async Task<ActionResult<List<PromptInfo>>> GetPromptsByProcessId(Guid processId)
    {
        var prompts = await _promptInfoService.GetPromptsByProcessIdAsync(processId);
        if (prompts.Count == 0)
        {
            return NotFound();
        }

        return Ok(prompts);
    }

    /// <summary>
    /// Gets the prompt by identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The prompt. 
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When no prompts could be found using the id provided
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(PromptInfo))]
    public async Task<ActionResult<PromptInfo>> GetPromptById(Guid id)
    {
        var prompt = await _promptInfoService.GetPromptByIdAsync(id);
        if (prompt == null)
        {
            return NotFound();
        }

        return Ok(prompt);
    }

    /// <summary>
    /// Creates a new prompt.
    /// </summary>
    /// <param name="promptInfo">The prompt information.</param>
    /// <returns>Created status.
    /// Produces Status Codes:
    ///     201 Created: When completed sucessfully
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces(typeof(PromptInfo))]
    public async Task<ActionResult<PromptInfo>> CreatePrompt([FromBody] PromptInfo promptInfo)
    {
        await _promptInfoService.AddPromptAsync(promptInfo);
        return Created();
    }

    /// <summary>
    /// Updates the prompt.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="promptInfo">The prompt information.</param>
    /// <returns>An accepted result.
    /// Produces Status Codes:
    ///     202 Accepted: When updated sucessfully
    ///     400 Bad Request: When the id provided doesn't match the id on the PromptInfo
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<ActionResult> UpdatePrompt(Guid id, [FromBody] PromptInfo promptInfo)
    {
        if (id != promptInfo.Id)
        {
            return BadRequest();
        }

        await _promptInfoService.UpdatePromptAsync(promptInfo);
        return Accepted();
    }

    /// <summary>
    /// Deletes the prompt.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>A no content result.
    /// Produces Status Codes:
    ///     204 No Content: When the prompt is successfully deleted
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeletePrompt(Guid id)
    {
        await _promptInfoService.DeletePromptAsync(id);
        return NoContent();
    }
}
