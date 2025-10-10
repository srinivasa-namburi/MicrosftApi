// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Scriban;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing system-wide prompts (Flow AI Assistant prompts).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SystemPromptController : ControllerBase
{
    private readonly ISystemPromptInfoService _systemPromptInfoService;
    private readonly ILogger<SystemPromptController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPromptController"/> class.
    /// </summary>
    /// <param name="systemPromptInfoService">Service for managing system prompts.</param>
    /// <param name="logger">Logger instance.</param>
    public SystemPromptController(
        ISystemPromptInfoService systemPromptInfoService,
        ILogger<SystemPromptController> logger)
    {
        _systemPromptInfoService = systemPromptInfoService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a system prompt by name (with database override if exists).
    /// </summary>
    /// <param name="promptName">The name of the prompt (e.g., "FlowUserConversationSystemPrompt").</param>
    /// <returns>The prompt text and metadata.</returns>
    [HttpGet("{promptName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemPromptResponse>> GetPromptAsync(string promptName)
    {
        var promptText = await _systemPromptInfoService.GetPromptAsync(promptName);
        if (promptText == null)
        {
            return NotFound($"System prompt '{promptName}' not found");
        }

        var defaultText = GetDefaultPromptByName(promptName);
        var isCustomized = !string.Equals(promptText, defaultText, StringComparison.Ordinal);

        return Ok(new SystemPromptResponse
        {
            Name = promptName,
            Text = promptText,
            IsCustomized = isCustomized
        });
    }

    /// <summary>
    /// Gets the default (non-overridden) prompt text.
    /// </summary>
    /// <param name="promptName">The name of the prompt.</param>
    /// <returns>The default prompt text.</returns>
    [HttpGet("{promptName}/default")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<SystemPromptResponse> GetDefaultPrompt(string promptName)
    {
        var defaultText = GetDefaultPromptByName(promptName);
        if (defaultText == null)
        {
            return NotFound($"Default system prompt '{promptName}' not found");
        }

        return Ok(new SystemPromptResponse
        {
            Name = promptName,
            Text = defaultText,
            IsCustomized = false
        });
    }

    /// <summary>
    /// Updates or creates a system prompt override.
    /// </summary>
    /// <param name="promptName">The name of the prompt.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated prompt ID.</returns>
    [HttpPut("{promptName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> UpdatePromptAsync(string promptName, [FromBody] UpdateSystemPromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Prompt text cannot be empty");
        }

        // Validate required variables are present
        var requiredVariables = GetRequiredVariablesForPrompt(promptName);
        if (requiredVariables.Any())
        {
            var validation = ValidatePromptVariables(request.Text, requiredVariables);
            if (!validation.IsValid)
            {
                return BadRequest($"Prompt validation failed: {validation.ErrorMessage}");
            }
        }

        try
        {
            var id = await _systemPromptInfoService.UpsertPromptOverrideAsync(promptName, request.Text, request.IsActive);
            _logger.LogInformation("System prompt '{PromptName}' updated successfully (ID: {Id})", promptName, id);
            return Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update system prompt '{PromptName}'", promptName);
            return BadRequest($"Failed to update prompt: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a system prompt override (reverts to default).
    /// </summary>
    /// <param name="promptName">The name of the prompt.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("{promptName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePromptOverrideAsync(string promptName)
    {
        var deleted = await _systemPromptInfoService.DeletePromptOverrideAsync(promptName);
        if (!deleted)
        {
            return NotFound($"No override exists for prompt '{promptName}'");
        }

        _logger.LogInformation("System prompt override '{PromptName}' deleted successfully", promptName);
        return Ok();
    }

    /// <summary>
    /// Validates that a prompt contains all required Scriban variables.
    /// </summary>
    /// <param name="promptName">The name of the prompt.</param>
    /// <param name="request">The validation request.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("{promptName}/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PromptValidationResult> ValidatePrompt(string promptName, [FromBody] ValidatePromptRequest request)
    {
        var requiredVariables = GetRequiredVariablesForPrompt(promptName);
        var result = ValidatePromptVariables(request.Text, requiredVariables);
        return Ok(result);
    }

    /// <summary>
    /// Gets all available system prompt names.
    /// </summary>
    /// <returns>List of prompt names.</returns>
    [HttpGet("available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetAvailablePrompts()
    {
        var prompts = new List<string>
        {
            PromptNames.FlowUserConversationSystemPrompt,
            PromptNames.FlowBackendConversationSystemPrompt,
            PromptNames.FlowIntentDetectionPrompt,
            PromptNames.FlowResponseSynthesisPrompt
        };

        return Ok(prompts);
    }

    #region Private Helpers

    private static string? GetDefaultPromptByName(string promptName)
    {
        var templatesCatalogType = typeof(SystemWidePromptCatalogTemplates);
        var property = templatesCatalogType.GetProperty(promptName);

        if (property == null || property.PropertyType != typeof(string))
        {
            return null;
        }

        return property.GetValue(null) as string;
    }

    private static List<string> GetRequiredVariablesForPrompt(string promptName)
    {
        // Define required variables for each prompt based on SystemWidePromptCatalogTemplates
        return promptName switch
        {
            nameof(PromptNames.FlowIntentDetectionPrompt) => new List<string> { "query", "availableProcesses" },
            nameof(PromptNames.FlowResponseSynthesisPrompt) => new List<string> { "query", "responses" },
            _ => new List<string>() // No required variables for system prompts
        };
    }

    private static PromptValidationResult ValidatePromptVariables(string promptText, List<string> requiredVariables)
    {
        if (!requiredVariables.Any())
        {
            return new PromptValidationResult { IsValid = true };
        }

        // Parse with Scriban to check for syntax errors
        try
        {
            var template = Template.Parse(promptText);
            if (template.HasErrors)
            {
                return new PromptValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Scriban parsing errors: {string.Join(", ", template.Messages.Select(m => m.Message))}"
                };
            }
        }
        catch (Exception ex)
        {
            return new PromptValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Failed to parse prompt template: {ex.Message}"
            };
        }

        // Check for required variables using regex (simple check for {{variable}} or {variable})
        var missingVariables = new List<string>();
        foreach (var variable in requiredVariables)
        {
            // Match {variable} or {{variable}} or {{ variable }}
            var pattern = @"\{\{?\s*" + Regex.Escape(variable) + @"\s*\}?\}";
            if (!Regex.IsMatch(promptText, pattern))
            {
                missingVariables.Add(variable);
            }
        }

        if (missingVariables.Any())
        {
            return new PromptValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Missing required variables: {string.Join(", ", missingVariables)}",
                MissingVariables = missingVariables
            };
        }

        return new PromptValidationResult { IsValid = true };
    }

    #endregion
}
