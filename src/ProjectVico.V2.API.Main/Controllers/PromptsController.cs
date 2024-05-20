using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/prompts")]
public class PromptsController : BaseController
{
    private readonly IPromptInfoService _promptInfoService;
    private readonly IMapper _mapper;

    public PromptsController(IPromptInfoService promptInfoService, IMapper mapper)
    {
        _promptInfoService = promptInfoService;
        _mapper = mapper;
    }

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

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces(typeof(PromptInfo))]
    public async Task<ActionResult<PromptInfo>> CreatePrompt([FromBody] PromptInfo promptInfo)
    {
        await _promptInfoService.AddPromptAsync(promptInfo);
        return CreatedAtAction(nameof(GetPromptById), new { promptId = promptInfo.Id }, promptInfo);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdatePrompt(Guid id, [FromBody] PromptInfo promptInfo)
    {
        if (id != promptInfo.Id)
        {
            return BadRequest();
        }

        await _promptInfoService.UpdatePromptAsync(promptInfo);
        return AcceptedAtAction(nameof(GetPromptById), new { promptId = promptInfo.Id }, promptInfo);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePrompt(Guid id)
    {
        await _promptInfoService.DeletePromptAsync(id);
        return NoContent();
    }
}
