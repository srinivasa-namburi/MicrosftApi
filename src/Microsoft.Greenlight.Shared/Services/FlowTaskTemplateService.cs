// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FlowTasks;
using System.Text.Json;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service for managing Flow Task templates.
/// </summary>
public class FlowTaskTemplateService : IFlowTaskTemplateService
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<FlowTaskTemplateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskTemplateService"/> class.
    /// </summary>
    public FlowTaskTemplateService(
        IMapper mapper,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<FlowTaskTemplateService> logger)
    {
        _mapper = mapper;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<FlowTaskTemplateInfo>> GetActiveFlowTaskTemplatesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var templates = await dbContext.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Where(t => t.IsActive)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<FlowTaskTemplateInfo>>(templates);
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateInfo?> GetFlowTaskTemplateByIdAsync(Guid templateId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var template = await dbContext.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
        {
            return null;
        }

        return _mapper.Map<FlowTaskTemplateInfo>(template);
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateInfo?> GetFlowTaskTemplateByNameAsync(string name)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var template = await dbContext.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name);

        if (template == null)
        {
            return null;
        }

        return _mapper.Map<FlowTaskTemplateInfo>(template);
    }

    /// <inheritdoc/>
    public async Task<List<FlowTaskTemplateInfo>> GetFlowTaskTemplatesByCategoryAsync(string category)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var templates = await dbContext.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Where(t => t.IsActive && t.Category == category)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<FlowTaskTemplateInfo>>(templates);
    }

    /// <inheritdoc/>
    public async Task<Guid> SyncFlowTaskFromDocumentProcessAsync(Guid documentProcessId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Load Document Process with metadata fields
        var documentProcess = await dbContext.DynamicDocumentProcessDefinitions
            .Include(dp => dp.MetaDataFields)
            .FirstOrDefaultAsync(dp => dp.Id == documentProcessId, cancellationToken);

        if (documentProcess == null)
        {
            throw new InvalidOperationException($"Document Process with ID {documentProcessId} not found.");
        }

        // Generate unique name for Flow Task template
        var templateName = $"DocumentGeneration_{documentProcess.ShortName}";

        // Check if template already exists - load without tracking to avoid concurrency issues
        var existingTemplateId = await dbContext.FlowTaskTemplates
            .Where(t => t.Name == templateName)
            .Select(t => new { t.Id, t.Version })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        FlowTaskTemplate template;
        Guid sectionId;

        if (existingTemplateId != null)
        {
            // Load template for update
            template = await dbContext.FlowTaskTemplates
                .FirstOrDefaultAsync(t => t.Id == existingTemplateId.Id, cancellationToken);

            if (template == null)
            {
                throw new InvalidOperationException($"Flow Task template {existingTemplateId.Id} not found.");
            }

            // Update template properties
            template.DisplayName = $"Generate {documentProcess.ShortName}";
            template.Description = documentProcess.Description ?? $"Generate document for {documentProcess.ShortName}";
            template.Category = "DocumentGeneration";
            template.TriggerPhrases = new[] { $"generate {documentProcess.ShortName.ToLower()}", $"create {documentProcess.ShortName.ToLower()}" };
            template.InitialPrompt = $"Let's generate a {documentProcess.ShortName} document. I'll collect the required information from you.";
            template.CompletionMessage = $"Your {documentProcess.ShortName} document has been generated successfully!";
            template.ModifiedUtc = DateTime.UtcNow;

            // Get or create the main section
            var existingSection = await dbContext.FlowTaskSections
                .FirstOrDefaultAsync(s => s.FlowTaskTemplateId == template.Id && s.Name == "DocumentFields", cancellationToken);

            if (existingSection != null)
            {
                sectionId = existingSection.Id;

                // Explicitly delete all existing requirements for this section
                var existingRequirements = await dbContext.FlowTaskRequirements
                    .Where(r => r.FlowTaskSectionId == sectionId)
                    .ToListAsync(cancellationToken);

                if (existingRequirements.Count > 0)
                {
                    dbContext.FlowTaskRequirements.RemoveRange(existingRequirements);
                }
            }
            else
            {
                // Create new section
                var newSection = new FlowTaskSection
                {
                    Id = Guid.NewGuid(),
                    FlowTaskTemplateId = template.Id,
                    Name = "DocumentFields",
                    DisplayName = "Document Fields",
                    Description = "Required information for document generation",
                    SortOrder = 0,
                    IsRequired = true
                };
                dbContext.FlowTaskSections.Add(newSection);
                sectionId = newSection.Id;
            }
        }
        else
        {
            // Create new template
            template = new FlowTaskTemplate
            {
                Id = Guid.NewGuid(),
                Name = templateName,
                DisplayName = $"Generate {documentProcess.ShortName}",
                Description = documentProcess.Description ?? $"Generate document for {documentProcess.ShortName}",
                Category = "DocumentGeneration",
                TriggerPhrases = new[] { $"generate {documentProcess.ShortName.ToLower()}", $"create {documentProcess.ShortName.ToLower()}" },
                InitialPrompt = $"Let's generate a {documentProcess.ShortName} document. I'll collect the required information from you.",
                CompletionMessage = $"Your {documentProcess.ShortName} document has been generated successfully!",
                IsActive = true,
                Version = "1.0.0"
            };

            dbContext.FlowTaskTemplates.Add(template);

            var section = new FlowTaskSection
            {
                Id = Guid.NewGuid(),
                FlowTaskTemplateId = template.Id,
                Name = "DocumentFields",
                DisplayName = "Document Fields",
                Description = "Required information for document generation",
                SortOrder = 0,
                IsRequired = true
            };

            dbContext.FlowTaskSections.Add(section);
            sectionId = section.Id;
        }

        // Map metadata fields to requirements
        var sortOrder = 0;
        foreach (var metadataField in documentProcess.MetaDataFields.OrderBy(f => f.Order))
        {
            var dataType = MapMetadataFieldTypeToFlowTaskDataType(metadataField.FieldType);

            var requirement = new FlowTaskRequirement
            {
                Id = Guid.NewGuid(),
                FlowTaskSectionId = sectionId,
                FieldName = metadataField.Name,
                DisplayName = metadataField.DisplayName,
                Description = metadataField.DescriptionToolTip,
                DataType = dataType.ToString().ToLower(),
                IsRequired = metadataField.IsRequired,
                IsDataSourced = false,
                DefaultValue = metadataField.DefaultValue,
                SortOrder = sortOrder++
            };

            // Map possible values for choice fields
            if (metadataField.HasPossibleValues && metadataField.PossibleValues.Count > 0)
            {
                requirement.ValidOptionsJson = JsonSerializer.Serialize(metadataField.PossibleValues);
            }

            dbContext.FlowTaskRequirements.Add(requirement);
        }

        // Add DocumentTitle as a required field for document generation
        var documentTitleRequirement = new FlowTaskRequirement
        {
            Id = Guid.NewGuid(),
            FlowTaskSectionId = sectionId,
            FieldName = "DocumentTitle",
            DisplayName = "Document Title",
            Description = "Title for the generated document",
            DataType = FlowTaskDataType.Text.ToString().ToLower(),
            IsRequired = true,
            IsDataSourced = false,
            SortOrder = sortOrder++
        };
        dbContext.FlowTaskRequirements.Add(documentTitleRequirement);

        // Create or update output template
        var existingOutputTemplate = await dbContext.FlowTaskOutputTemplates
            .FirstOrDefaultAsync(ot => ot.FlowTaskTemplateId == template.Id && ot.OutputType == "DocumentGeneration", cancellationToken);

        if (existingOutputTemplate == null)
        {
            var outputTemplate = new FlowTaskOutputTemplate
            {
                Id = Guid.NewGuid(),
                FlowTaskTemplateId = template.Id,
                Name = "Generated Document Link",
                OutputType = "DocumentGeneration",
                TemplateContent = "Document generation link will be provided upon completion.",
                ContentType = "text/plain",
                ExecutionOrder = 0,
                IsRequired = true
            };
            dbContext.FlowTaskOutputTemplates.Add(outputTemplate);
        }
        else
        {
            existingOutputTemplate.ModifiedUtc = DateTime.UtcNow;
        }

        // Save all changes in one transaction
        await dbContext.SaveChangesAsync(cancellationToken);

        // Update Document Process with FlowTaskTemplateId if not already set
        if (documentProcess.FlowTaskTemplateId != template.Id)
        {
            documentProcess.FlowTaskTemplateId = template.Id;
            documentProcess.ModifiedUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Synced Flow Task template {TemplateId} from Document Process {ProcessId} with {RequirementCount} requirements",
            template.Id,
            documentProcessId,
            sortOrder);

        return template.Id;
    }

    /// <summary>
    /// Maps a Document Process metadata field type to a Flow Task data type.
    /// </summary>
    private static FlowTaskDataType MapMetadataFieldTypeToFlowTaskDataType(DynamicDocumentProcessMetaDataFieldType fieldType)
    {
        return fieldType switch
        {
            DynamicDocumentProcessMetaDataFieldType.Text => FlowTaskDataType.Text,
            DynamicDocumentProcessMetaDataFieldType.MultilineText => FlowTaskDataType.TextArea,
            DynamicDocumentProcessMetaDataFieldType.Number => FlowTaskDataType.Number,
            DynamicDocumentProcessMetaDataFieldType.Date => FlowTaskDataType.Date,
            DynamicDocumentProcessMetaDataFieldType.Time => FlowTaskDataType.Text,
            DynamicDocumentProcessMetaDataFieldType.DateTime => FlowTaskDataType.DateTime,
            DynamicDocumentProcessMetaDataFieldType.BooleanCheckbox => FlowTaskDataType.Boolean,
            DynamicDocumentProcessMetaDataFieldType.BooleanSwitchToggle => FlowTaskDataType.Boolean,
            DynamicDocumentProcessMetaDataFieldType.File => FlowTaskDataType.File,
            DynamicDocumentProcessMetaDataFieldType.MultiSelectWithPossibleValues => FlowTaskDataType.MultiChoice,
            DynamicDocumentProcessMetaDataFieldType.SelectRadioButton => FlowTaskDataType.Choice,
            DynamicDocumentProcessMetaDataFieldType.SelectDropdown => FlowTaskDataType.Choice,
            DynamicDocumentProcessMetaDataFieldType.MapComponent => FlowTaskDataType.Json,
            _ => FlowTaskDataType.Text
        };
    }
}
