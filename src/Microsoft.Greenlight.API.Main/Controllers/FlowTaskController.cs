// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.FlowTasks;
using Microsoft.Greenlight.Grains.Chat.Contracts;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing Flow Task templates.
/// </summary>
[Route("/api/flow-tasks")]
public class FlowTaskController : BaseController
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IMapper _mapper;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<FlowTaskController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskController"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="grainFactory">The Orleans grain factory.</param>
    /// <param name="logger">The logger.</param>
    public FlowTaskController(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IMapper mapper,
        IGrainFactory grainFactory,
        ILogger<FlowTaskController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets all Flow Task templates with optional filtering and pagination.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="isActive">Optional filter for active/inactive templates.</param>
    /// <param name="skip">Number of records to skip for pagination.</param>
    /// <param name="take">Number of records to take for pagination.</param>
    /// <returns>A list of Flow Task template summary information.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<FlowTaskTemplateInfo>>]
    public async Task<ActionResult<List<FlowTaskTemplateInfo>>> GetAllFlowTaskTemplates(
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var query = db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        var templates = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var templateInfos = templates.Select(t => _mapper.Map<FlowTaskTemplateInfo>(t)).ToList();

        return Ok(templateInfos);
    }

    /// <summary>
    /// Gets a Flow Task template by its ID with full details.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>The Flow Task template with full details.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    ///     404 Not Found: When the template was not found
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<FlowTaskTemplateDetailDto>]
    public async Task<ActionResult<FlowTaskTemplateDetailDto>> GetFlowTaskTemplateById(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var template = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        var detailDto = _mapper.Map<FlowTaskTemplateDetailDto>(template);
        return Ok(detailDto);
    }

    /// <summary>
    /// Gets distinct categories from all Flow Task templates.
    /// </summary>
    /// <returns>A list of distinct categories.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    /// </returns>
    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<string>>]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var categories = await db.FlowTaskTemplates
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>
    /// Creates a new Flow Task template.
    /// </summary>
    /// <param name="templateDto">The Flow Task template details.</param>
    /// <returns>The created Flow Task template.
    /// Produces Status Codes:
    ///     201 Created: When completed successfully
    ///     400 Bad Request: When the template data is invalid
    /// </returns>
    [HttpPost]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<FlowTaskTemplateDetailDto>]
    public async Task<ActionResult<FlowTaskTemplateDetailDto>> CreateFlowTaskTemplate([FromBody] FlowTaskTemplateDetailDto templateDto)
    {
        if (templateDto == null)
        {
            return BadRequest("Template data cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(templateDto.Name))
        {
            return BadRequest("Template name is required.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Check if a template with the same name already exists
        var existingTemplate = await db.FlowTaskTemplates
            .FirstOrDefaultAsync(t => t.Name == templateDto.Name);

        if (existingTemplate != null)
        {
            return BadRequest($"A template with name '{templateDto.Name}' already exists.");
        }

        // Map DTO to entity
        var template = _mapper.Map<FlowTaskTemplate>(templateDto);
        template.Id = Guid.NewGuid();
        template.CreatedUtc = DateTime.UtcNow;
        template.ModifiedUtc = DateTime.UtcNow;

        db.FlowTaskTemplates.Add(template);
        await db.SaveChangesAsync();

        _logger.LogInformation("Created Flow Task template {TemplateId} with name '{TemplateName}'", template.Id, template.Name);

        // Reload with includes to return full details
        var createdTemplate = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .AsNoTracking()
            .FirstAsync(t => t.Id == template.Id);

        var resultDto = _mapper.Map<FlowTaskTemplateDetailDto>(createdTemplate);
        return CreatedAtAction(nameof(GetFlowTaskTemplateById), new { id = template.Id }, resultDto);
    }

    /// <summary>
    /// Updates an existing Flow Task template.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="templateDto">The updated Flow Task template details.</param>
    /// <returns>The updated Flow Task template.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    ///     400 Bad Request: When the template data is invalid or IDs don't match
    ///     404 Not Found: When the template was not found
    /// </returns>
    [HttpPut("{id:guid}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<FlowTaskTemplateDetailDto>]
    public async Task<ActionResult<FlowTaskTemplateDetailDto>> UpdateFlowTaskTemplate(Guid id, [FromBody] FlowTaskTemplateDetailDto templateDto)
    {
        if (templateDto == null || id != templateDto.Id)
        {
            return BadRequest("Invalid template data or ID mismatch.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var existingTemplate = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (existingTemplate == null)
        {
            return NotFound();
        }

        // Remove old child entities
        db.FlowTaskSections.RemoveRange(existingTemplate.Sections);
        db.FlowTaskOutputTemplates.RemoveRange(existingTemplate.OutputTemplates);
        db.FlowTaskDataSources.RemoveRange(existingTemplate.DataSources);

        // Update template properties
        existingTemplate.Name = templateDto.Name;
        existingTemplate.DisplayName = templateDto.DisplayName;
        existingTemplate.Description = templateDto.Description;
        existingTemplate.Category = templateDto.Category;
        existingTemplate.TriggerPhrases = templateDto.TriggerPhrases;
        existingTemplate.InitialPrompt = templateDto.InitialPrompt;
        existingTemplate.CompletionMessage = templateDto.CompletionMessage;
        existingTemplate.IsActive = templateDto.IsActive;
        existingTemplate.Version = templateDto.Version;
        existingTemplate.MetadataJson = templateDto.MetadataJson;
        existingTemplate.ModifiedUtc = DateTime.UtcNow;

        // Map and add new child entities
        existingTemplate.Sections = _mapper.Map<List<FlowTaskSection>>(templateDto.Sections);
        existingTemplate.OutputTemplates = _mapper.Map<List<FlowTaskOutputTemplate>>(templateDto.OutputTemplates);
        existingTemplate.DataSources = _mapper.Map<List<FlowTaskDataSource>>(templateDto.DataSources);

        await db.SaveChangesAsync();

        _logger.LogInformation("Updated Flow Task template {TemplateId}", id);

        // Reload with fresh data
        var updatedTemplate = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .AsNoTracking()
            .FirstAsync(t => t.Id == id);

        var resultDto = _mapper.Map<FlowTaskTemplateDetailDto>(updatedTemplate);
        return Ok(resultDto);
    }

    /// <summary>
    /// Deletes a Flow Task template.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>No content if successful.
    /// Produces Status Codes:
    ///     204 No Content: When completed successfully
    ///     404 Not Found: When the template was not found
    /// </returns>
    [HttpDelete("{id:guid}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFlowTaskTemplate(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var template = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        db.FlowTaskTemplates.Remove(template);
        await db.SaveChangesAsync();

        _logger.LogInformation("Deleted Flow Task template {TemplateId} with name '{TemplateName}'", id, template.Name);

        return NoContent();
    }

    /// <summary>
    /// Tests a Flow Task template execution with provided test message.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="testMessage">Optional test message to initiate the flow.</param>
    /// <returns>The test execution result.
    /// Produces Status Codes:
    ///     200 OK: When test completed successfully
    ///     404 Not Found: When the template was not found
    ///     500 Internal Server Error: When test execution fails
    /// </returns>
    [HttpPost("{id:guid}/test")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> TestFlowTaskTemplate(Guid id, [FromBody] string? testMessage = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var template = await db.FlowTaskTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        try
        {
            // Create a test execution grain
            var executionId = Guid.NewGuid();
            var flowSessionId = Guid.NewGuid();
            var grain = _grainFactory.GetGrain<IFlowTaskAgenticExecutionGrain>(executionId);

            // Start execution with the template
            var result = await grain.StartExecutionAsync(
                flowSessionId,
                id,
                testMessage ?? "Test execution",
                "Test context"
            );

            _logger.LogInformation("Started test execution {ExecutionId} for template {TemplateId}", executionId, id);

            return Ok(new
            {
                ExecutionId = executionId,
                FlowSessionId = flowSessionId,
                TemplateId = id,
                TemplateName = template.Name,
                State = result.State.ToString(),
                ResponseMessage = result.ResponseMessage,
                IsComplete = result.IsComplete,
                CurrentSection = result.CurrentSection,
                CurrentRequirement = result.CurrentRequirement,
                ProgressPercentage = result.ProgressPercentage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test template {TemplateId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, $"Test execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports a Flow Task template from JSON.
    /// </summary>
    /// <param name="importData">The template data to import.</param>
    /// <returns>The imported Flow Task template.
    /// Produces Status Codes:
    ///     201 Created: When import completed successfully
    ///     400 Bad Request: When the import data is invalid
    /// </returns>
    [HttpPost("import")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<FlowTaskTemplateDetailDto>]
    public async Task<ActionResult<FlowTaskTemplateDetailDto>> ImportFlowTaskTemplate([FromBody] FlowTaskTemplateDetailDto importData)
    {
        if (importData == null)
        {
            return BadRequest("Import data cannot be null.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Generate new IDs for imported template
        var template = _mapper.Map<FlowTaskTemplate>(importData);
        template.Id = Guid.NewGuid();
        template.CreatedUtc = DateTime.UtcNow;
        template.ModifiedUtc = DateTime.UtcNow;

        // Check for name conflicts and generate unique name if needed
        var baseName = template.Name;
        var counter = 1;
        while (await db.FlowTaskTemplates.AnyAsync(t => t.Name == template.Name))
        {
            template.Name = $"{baseName}_imported_{counter}";
            counter++;
        }

        db.FlowTaskTemplates.Add(template);
        await db.SaveChangesAsync();

        _logger.LogInformation("Imported Flow Task template {TemplateId} with name '{TemplateName}'", template.Id, template.Name);

        // Reload with full details
        var importedTemplate = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .AsNoTracking()
            .FirstAsync(t => t.Id == template.Id);

        var resultDto = _mapper.Map<FlowTaskTemplateDetailDto>(importedTemplate);
        return CreatedAtAction(nameof(GetFlowTaskTemplateById), new { id = template.Id }, resultDto);
    }

    /// <summary>
    /// Exports a Flow Task template as JSON.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>The exported Flow Task template as JSON.
    /// Produces Status Codes:
    ///     200 OK: When export completed successfully
    ///     404 Not Found: When the template was not found
    /// </returns>
    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<FlowTaskTemplateDetailDto>]
    public async Task<ActionResult<FlowTaskTemplateDetailDto>> ExportFlowTaskTemplate(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var template = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
                .ThenInclude(ds => ((FlowTaskMcpToolDataSource)ds).Parameters)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        var exportDto = _mapper.Map<FlowTaskTemplateDetailDto>(template);

        _logger.LogInformation("Exported Flow Task template {TemplateId}", id);

        return Ok(exportDto);
    }
}
