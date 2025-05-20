using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using System.Text.RegularExpressions;

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
    [Produces("application/json")]
    [Produces<List<PromptInfo>>]
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
    [Produces("application/json")]
    [Produces<PromptInfo>]
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
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<PromptInfo>]
    public async Task<ActionResult<PromptInfo>> CreatePrompt([FromBody] PromptInfo promptInfo)
    {
        var id = await _promptInfoService.AddPromptAsync(promptInfo);
        return Created($"/api/prompts/{id}", promptInfo);
    }

    /// <summary>
    /// Updates the prompt.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="promptInfo">The prompt information.</param>
    /// <returns>An accepted result.
    /// Produces Status Codes:
    ///     200 Ok: When updated sucessfully
    ///     400 Bad Request: When the id provided doesn't match the id on the PromptInfo
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<PromptInfo>]
    public async Task<ActionResult> UpdatePrompt(Guid id, [FromBody] PromptInfo promptInfo)
    {
        if (id != promptInfo.Id)
        {
            return BadRequest();
        }

        await _promptInfoService.UpdatePromptAsync(promptInfo);
        return Ok(promptInfo);
    }

    /// <summary>
    /// Returns a list of variables that are required for a given prompt using reflection and pattern matching
    /// on the default prompt catalog
    /// </summary>
    /// <param name="promptName"></param>
    /// <returns></returns>
    [HttpGet("{promptName}/variables")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<string>>]
    public ActionResult<List<string>> GetRequiredPromptVariablesForPromptName(string promptName)
    {
        var defaultPrompts = new DefaultPromptCatalogTypes();

        // There are string Properties on the DefaultPromptCatalogTypes class that are the names of the prompts
        // We can use reflection to get the value of the property with the name of the promptName

        var prompt = defaultPrompts.GetType().GetProperty(promptName)?.GetValue(defaultPrompts) as string;

        if (string.IsNullOrEmpty(prompt))
        {
            return NotFound();
        }

        var matches = Regex.Matches(prompt, @"\{\{ ([^\}]+) \}\}");
        // Removed duplicates from the matches, as well as trimming spaces and curly braces
        if (matches.Count > 0) {
            var matchedStrings = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
            return Ok(matchedStrings);
        }
        else
        {
            return NotFound();
        }
        
    }

    /// <summary>
    /// Gets the default prompt text for a given prompt short code from DefaultPromptCatalogTypes.
    /// </summary>
    /// <param name="shortCode">The short code of the prompt.</param>
    /// <returns>The default prompt text.
    /// Produces Status Codes:
    ///     200 OK: When default prompt text is found
    ///     404 Not Found: When no default prompt exists with the given short code
    /// </returns>
    [HttpGet("default/{shortCode}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public ActionResult<string> GetDefaultPromptText(string shortCode)
    {
        var defaultPrompts = new DefaultPromptCatalogTypes();
        var property = defaultPrompts.GetType().GetProperty(shortCode);
        
        if (property == null)
        {
            return NotFound();
        }

        var defaultPromptText = property.GetValue(defaultPrompts) as string;
        
        if (string.IsNullOrEmpty(defaultPromptText))
        {
            return NotFound();
        }

        return Ok(defaultPromptText);
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
