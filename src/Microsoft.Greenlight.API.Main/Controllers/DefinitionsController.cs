// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Definitions;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Prompts;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Import/export controller for document process and document library definitions.
/// </summary>
[Route("api/definitions")]
public sealed class DefinitionsController :BaseController
{
    private readonly DocGenerationDbContext _db;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly ILogger<DefinitionsController> _logger;
    private readonly ISemanticKernelVectorStoreProvider _vectorStoreProvider;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

    // Lightweight DTO defined locally to decouple API from shared contracts for this diagnostic endpoint
    public sealed class IndexCompatibilityInfoDto
    {
        public string IndexName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool IsSkLayout { get; set; }
        public int? MatchedEmbeddingDimensions { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? Error { get; set; }
    }

    public DefinitionsController(
        DocGenerationDbContext db,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        ILogger<DefinitionsController> logger,
        ISemanticKernelVectorStoreProvider vectorStoreProvider,
        AzureFileHelper azureFileHelper)
    {
        _db = db;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _logger = logger;
        _vectorStoreProvider = vectorStoreProvider;
        _azureFileHelper = azureFileHelper;
        _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
    }

    // ---------- Process export/import ----------

    /// <summary>
    /// Exports a document process definition into a portable package.
    /// </summary>
    [HttpGet("process/{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(DocumentProcessDefinitionPackageDto))]
    public async Task<ActionResult<DocumentProcessDefinitionPackageDto>> ExportProcess(Guid id)
    {
        var process = await LoadProcessForExportAsync(id);
        if (process == null)
        {
            return NotFound();
        }

        var promptDtos = BuildPromptPackageDtos(process.Prompts);
        var metaDtos = BuildMetaDataPackageDtos(process.MetaDataFields);
        var outlineDto = BuildOutlinePackageDto(process.DocumentOutline);

        var pkg = new DocumentProcessDefinitionPackageDto
        {
            OriginalProcessId = process.Id,
            ShortName = process.ShortName,
            Description = process.Description,
            PrecedingSearchPartitionInclusionCount = process.PrecedingSearchPartitionInclusionCount,
            FollowingSearchPartitionInclusionCount = process.FollowingSearchPartitionInclusionCount,
            NumberOfCitationsToGetFromRepository = process.NumberOfCitationsToGetFromRepository,
            MinimumRelevanceForCitations = process.MinimumRelevanceForCitations,
            VectorStoreChunkSize = process.VectorStoreChunkSize,
            VectorStoreChunkOverlap = process.VectorStoreChunkOverlap,
            BlobStorageContainerName = process.BlobStorageContainerName,
            BlobStorageAutoImportFolderName = process.BlobStorageAutoImportFolderName,
            Prompts = promptDtos.Count == 0 ? null : promptDtos,
            Outline = outlineDto,
            MetaDataFields = metaDtos.Count == 0 ? null : metaDtos
        };

        return Ok(pkg);
    }

    // ---------- Export Helper Methods ----------

    private async Task<DynamicDocumentProcessDefinition?> LoadProcessForExportAsync(Guid id)
    {
        return await _db.DynamicDocumentProcessDefinitions
            .Include(p => p.Prompts).ThenInclude(pi => pi.PromptDefinition)
            .Include(p => p.DocumentOutline)!
                .ThenInclude(o => o.OutlineItems)
                    .ThenInclude(a => a.Children)
                        .ThenInclude(b => b.Children)
                            .ThenInclude(c => c.Children)
                                .ThenInclude(d => d.Children)
                                    .ThenInclude(e => e.Children)
            .Include(p => p.MetaDataFields)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    private static List<PromptImplementationPackageDto> BuildPromptPackageDtos(ICollection<PromptImplementation> prompts)
    {
        return prompts
            .Where(pi => pi.PromptDefinition != null)
            .Select(pi => new PromptImplementationPackageDto
            {
                OriginalPromptImplementationId = pi.Id,
                OriginalPromptDefinitionId = pi.PromptDefinitionId,
                ShortCode = pi.PromptDefinition!.ShortCode,
                Description = pi.PromptDefinition!.Description,
                Text = pi.Text
            }).ToList();
    }

    private static List<DocumentProcessMetaDataFieldPackageDto> BuildMetaDataPackageDtos(ICollection<DynamicDocumentProcessMetaDataField>? metaDataFields)
    {
        return (metaDataFields ?? [])
            .OrderBy(f => f.Order)
            .Select(f => new DocumentProcessMetaDataFieldPackageDto
            {
                OriginalId = f.Id,
                Name = f.Name,
                DisplayName = f.DisplayName,
                Description = f.DescriptionToolTip,
                Type = f.FieldType.ToString(),
                IsRequired = f.IsRequired,
                Order = f.Order,
                DefaultValue = f.DefaultValue,
                JsonSchema = null,
                HasPossibleValues = f.HasPossibleValues,
                PossibleValues = f.PossibleValues?.ToList() ?? new List<string>(),
                DefaultPossibleValue = f.DefaultPossibleValue
            }).ToList();
    }

    private static DocumentOutlinePackageDto? BuildOutlinePackageDto(DocumentOutline? documentOutline)
    {
        if (documentOutline == null)
        {
            return null;
        }

        // Flatten starting from the outline's root items using the navigation graph to ensure we gather all descendants
        var flat = new List<DocumentOutlineItem>();
        void Collect(DocumentOutlineItem item)
        {
            flat.Add(item);
            if (item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    Collect(child);
                }
            }
        }

        foreach (var root in documentOutline.OutlineItems ?? new List<DocumentOutlineItem>())
        {
            Collect(root);
        }

        return new DocumentOutlinePackageDto
        {
            OriginalDocumentOutlineId = documentOutline.Id,
            FullText = documentOutline.FullText,
            Items = BuildOutlinePackageItems(flat, parentId: null)
        };
    }

    // Build a hierarchy strictly from flat items using ParentId + OrderIndex. Do not rely on EF navigation properties.
    private static List<DocumentOutlineItemPackageDto> BuildOutlinePackageItems(List<DocumentOutlineItem> allItems, Guid? parentId)
    {
        var result = new List<DocumentOutlineItemPackageDto>();
        var siblings = allItems
            .Where(i => i.ParentId == parentId)
            // DB OrderIndex is 1-based; preserve exact value, but ensure nulls sort last by using large default
            .OrderBy(i => i.OrderIndex ?? int.MaxValue)
            .ToList();

        for (int s = 0; s < siblings.Count; s++)
        {
            var i = siblings[s];
            var dto = new DocumentOutlineItemPackageDto
            {
                OriginalId = i.Id,
                SectionNumber = i.SectionNumber,
                SectionTitle = i.SectionTitle,
                Level = i.Level,
                PromptInstructions = i.PromptInstructions,
                RenderTitleOnly = i.RenderTitleOnly,
                // Preserve 1-based OrderIndex from DB as-is
                OrderIndex = i.OrderIndex
            };

            dto.Children = BuildOutlinePackageItems(allItems, i.Id);
            result.Add(dto);
        }
        return result;
    }

    /// <summary>
    /// Imports a document process definition package. New IDs are generated.
    /// </summary>
    /// <param name="package">The document process package to import.</param>
    /// <param name="blobStorageContainerName">Optional override for blob storage container name.</param>
    /// <param name="blobStorageAutoImportFolderName">Optional override for auto import folder name.</param>
    /// <returns>The ID of the created document process.</returns>
    [HttpPost("process/import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> ImportProcess(
        [FromBody] DocumentProcessDefinitionPackageDto package,
        [FromQuery] string? blobStorageContainerName = null,
        [FromQuery] string? blobStorageAutoImportFolderName = null)
    {
        // Validate package and normalize short name
        var validationResult = ValidateAndNormalizePackage(package);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Check for conflicts
        var conflictResult = await CheckShortNameConflictAsync(package.ShortName!, _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync);
        if (conflictResult != null)
        {
            return conflictResult;
        }

        // Resolve container and auto-import folder, prioritizing DTO fields
        var effectiveContainer = !string.IsNullOrWhiteSpace(package.BlobStorageContainerName)
            ? package.BlobStorageContainerName!
            : (!string.IsNullOrWhiteSpace(blobStorageContainerName) ? blobStorageContainerName! : await PrepareContainerAsync(package.ShortName!));

        var effectiveAutoFolder = !string.IsNullOrWhiteSpace(package.BlobStorageAutoImportFolderName)
            ? package.BlobStorageAutoImportFolderName!
            : (!string.IsNullOrWhiteSpace(blobStorageAutoImportFolderName) ? blobStorageAutoImportFolderName! : "ingest-auto");

        // Create the document process
        var created = await CreateMinimalProcessAsync(package, effectiveContainer, effectiveAutoFolder);

        // Import outline if provided
        if (package.Outline != null)
        {
            await ImportOutlineAsync(package.Outline, created.Id);
        }

        // Import prompts if provided
        if (package.Prompts is { Count: > 0 })
        {
            await ImportPromptsAsync(package.Prompts, created.Id, package.ShortName!);
        }

        // Create missing prompt implementations
        await CreateMissingPromptImplementationsAsync(created.Id, package.ShortName!);

        // Import metadata fields if provided
        if (package.MetaDataFields != null && package.MetaDataFields.Count > 0)
        {
            await ImportMetaDataFieldsAsync(package.MetaDataFields, created.Id);
        }

        return Ok(created.Id);
    }

    // ---------- Import Helper Methods ----------

    private ActionResult? ValidateAndNormalizePackage<T>(T package) where T : class
    {
        if (package == null)
        {
            return BadRequest("Invalid package");
        }

        // Normalize short name for packages that have it
        var shortNameProperty = typeof(T).GetProperty("ShortName");
        if (shortNameProperty != null)
        {
            var shortName = shortNameProperty.GetValue(package) as string;
            var normalizedShortName = NormalizeShortName(shortName);
            
            if (string.IsNullOrWhiteSpace(normalizedShortName))
            {
                return BadRequest("Short Name is required and must contain only letters, digits, or periods.");
            }
            
            shortNameProperty.SetValue(package, normalizedShortName);
        }

        return null;
    }

    private async Task<ActionResult?> CheckShortNameConflictAsync<T>(string shortName, Func<string, Task<T?>> getByShortNameFunc) where T : class
    {
        var existing = await getByShortNameFunc(shortName);
        if (existing != null)
        {
            return Conflict($"Short name '{shortName}' is already in use.");
        }
        return null;
    }

    private async Task<string> PrepareContainerAsync(string shortName)
    {
        var containerName = SanitizeContainerName(shortName);

        if (await _azureFileHelper.ContainerExistsAsync(containerName))
        {
            var msg = $"Blob container '{containerName}' already exists and will be reused.";
            _logger.LogWarning(msg);
            Response.Headers.TryAdd("X-Warning", msg);
        }

        return containerName;
    }

    private async Task<DocumentProcessInfo> CreateMinimalProcessAsync(DocumentProcessDefinitionPackageDto package, string containerName, string autoImportFolderName)
    {
        // Clamp optional chunk values to sane ranges
        package.VectorStoreChunkSize = Clamp(package.VectorStoreChunkSize, 100, 8000);
        package.VectorStoreChunkOverlap = Clamp(package.VectorStoreChunkOverlap, 0, 2000);

        var newInfo = new DocumentProcessInfo
        {
            ShortName = package.ShortName,
            Description = package.Description,
            BlobStorageContainerName = containerName,
            BlobStorageAutoImportFolderName = autoImportFolderName,
            PrecedingSearchPartitionInclusionCount = package.PrecedingSearchPartitionInclusionCount,
            FollowingSearchPartitionInclusionCount = package.FollowingSearchPartitionInclusionCount,
            NumberOfCitationsToGetFromRepository = package.NumberOfCitationsToGetFromRepository,
            MinimumRelevanceForCitations = package.MinimumRelevanceForCitations,
            VectorStoreChunkSize = package.VectorStoreChunkSize,
            VectorStoreChunkOverlap = package.VectorStoreChunkOverlap
        };

        return await _documentProcessInfoService.CreateMinimalDocumentProcessInfoAsync(newInfo);
    }

    private async Task ImportOutlineAsync(DocumentOutlinePackageDto outlinePackage, Guid processId)
    {
        // Always create a completely new outline for the imported process
        var newOutline = new DocumentOutline
        {
            Id = Guid.NewGuid(),
            DocumentProcessDefinitionId = processId,
            OutlineItems = new List<DocumentOutlineItem>()
        };
        _db.DocumentOutlines.Add(newOutline);
        await _db.SaveChangesAsync();

        // Update the process to reference the new outline
        var createdProcess = await _db.DynamicDocumentProcessDefinitions
            .FirstOrDefaultAsync(p => p.Id == processId);
        
        if (createdProcess != null)
        {
            createdProcess.DocumentOutlineId = newOutline.Id;
            _db.DynamicDocumentProcessDefinitions.Update(createdProcess);
            await _db.SaveChangesAsync();
        }

        // Build new items with completely new IDs and proper hierarchy
        var newRootItems = BuildNewOutlineItems(outlinePackage.Items, newOutline.Id, null);
        
        // Add items level by level to ensure parents are saved before children
        await AddOutlineItemsLevelByLevel(newRootItems);
    }

    private async Task ImportPromptsAsync(List<PromptImplementationPackageDto> prompts, Guid processId, string processShortName)
    {
        var promptsProcessed = 0;
        var promptsSkipped = 0;

        foreach (var promptPackage in prompts)
        {
            if (string.IsNullOrWhiteSpace(promptPackage.ShortCode))
            {
                _logger.LogWarning("Skipping prompt import with empty ShortCode for process {ProcessShortName}", processShortName);
                promptsSkipped++;
                continue;
            }

            // Look for existing prompt definition by ShortCode (must already exist - system level)
            var promptDefinition = await _db.PromptDefinitions
                .FirstOrDefaultAsync(pd => pd.ShortCode == promptPackage.ShortCode);

            if (promptDefinition == null)
            {
                _logger.LogWarning("Skipping prompt implementation for '{ShortCode}' - Prompt Definition does not exist in system. " +
                                 "Prompt Definitions are system-level and must be created separately. Process: '{ProcessShortName}'", 
                                 promptPackage.ShortCode, processShortName);
                promptsSkipped++;
                continue;
            }

            await CreateOrUpdatePromptImplementationAsync(promptPackage, processId, promptDefinition, processShortName);
            promptsProcessed++;
        }

        // Save all prompt implementation changes in one transaction
        if (promptsProcessed > 0)
        {
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Prompt import completed for process '{ProcessShortName}': {ProcessedCount} processed, {SkippedCount} skipped", 
            processShortName, promptsProcessed, promptsSkipped);

        if (promptsSkipped > 0)
        {
            _logger.LogWarning("Some prompts were skipped during import for process '{ProcessShortName}'. " +
                             "Check that all required Prompt Definitions exist in the system.", processShortName);
        }
    }

    private async Task CreateOrUpdatePromptImplementationAsync(PromptImplementationPackageDto promptPackage, Guid processId, PromptDefinition promptDefinition, string processShortName)
    {
        // Check if this process already has an implementation for this prompt
        var existingImplementation = await _db.PromptImplementations
            .FirstOrDefaultAsync(pi => pi.DocumentProcessDefinitionId == processId && 
                                     pi.PromptDefinitionId == promptDefinition.Id);

        if (existingImplementation == null)
        {
            // Create new prompt implementation for this process
            var newImplementation = new PromptImplementation
            {
                Id = Guid.NewGuid(), // Always use new ID for prompt implementations
                DocumentProcessDefinitionId = processId,
                PromptDefinitionId = promptDefinition.Id,
                Text = promptPackage.Text ?? string.Empty
            };
            _db.PromptImplementations.Add(newImplementation);
            _logger.LogInformation("Created prompt implementation for '{ShortCode}' in process '{ProcessShortName}'", 
                promptPackage.ShortCode, processShortName);
        }
        else
        {
            // Update existing implementation text
            if (!string.Equals(existingImplementation.Text, promptPackage.Text, StringComparison.Ordinal))
            {
                existingImplementation.Text = promptPackage.Text ?? string.Empty;
                _db.PromptImplementations.Update(existingImplementation);
                _logger.LogInformation("Updated prompt implementation text for '{ShortCode}' in process '{ProcessShortName}'", 
                    promptPackage.ShortCode, processShortName);
            }
            else
            {
                _logger.LogDebug("Prompt implementation for '{ShortCode}' unchanged in process '{ProcessShortName}'", 
                    promptPackage.ShortCode, processShortName);
            }
        }
    }

    private async Task ImportMetaDataFieldsAsync(List<DocumentProcessMetaDataFieldPackageDto> fields, Guid processId)
    {
        foreach (var field in fields)
        {
            var typeEnum = Microsoft.Greenlight.Shared.Enums.DynamicDocumentProcessMetaDataFieldType.Text;
            Enum.TryParse(field.Type, ignoreCase: true, out typeEnum);

            var newField = new DynamicDocumentProcessMetaDataField
            {
                Id = Guid.NewGuid(), // Always use new ID, never the original
                DynamicDocumentProcessDefinitionId = processId,
                Name = field.Name,
                DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName,
                DescriptionToolTip = field.Description,
                FieldType = typeEnum,
                IsRequired = field.IsRequired,
                Order = field.Order,
                DefaultValue = field.DefaultValue,
                HasPossibleValues = field.HasPossibleValues || (field.PossibleValues != null && field.PossibleValues.Count > 0),
                PossibleValues = field.PossibleValues?.ToList() ?? new List<string>(),
                DefaultPossibleValue = field.DefaultPossibleValue
            };

            // If package does not carry an order, append to end
            if (newField.Order <= 0)
            {
                newField.Order = (await _db.DynamicDocumentProcessMetaDataFields.CountAsync(x => x.DynamicDocumentProcessDefinitionId == processId)) + 1;
            }

            await _db.DynamicDocumentProcessMetaDataFields.AddAsync(newField);
        }

        await _db.SaveChangesAsync();
    }

    private List<DocumentOutlineItem> BuildNewOutlineItems(List<DocumentOutlineItemPackageDto> items, Guid outlineId, Guid? parentId, int parentLevel = -1)
    {
        var result = new List<DocumentOutlineItem>();
        // Sort by provided OrderIndex if present; otherwise maintain current order
        var ordered = items
            .Select((it, idx) => new { it, idx })
            .OrderBy(x => x.it.OrderIndex ?? x.idx)
            .Select(x => x.it)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var dto = ordered[i];
            var newItemId = Guid.NewGuid();
            var level = parentLevel + 1; // compute level from parent

            var item = new DocumentOutlineItem
            {
                Id = newItemId,
                SectionNumber = dto.SectionNumber,
                SectionTitle = dto.SectionTitle,
                Level = level,
                PromptInstructions = dto.PromptInstructions,
                RenderTitleOnly = dto.RenderTitleOnly,
                ParentId = parentId,
                // IMPORTANT: Only root items belong in DocumentOutline.OutlineItems. Children must have null DocumentOutlineId.
                DocumentOutlineId = parentId == null ? outlineId : null,
                // DB uses 1-based ordering; preserve provided value or fallback to 1-based position
                OrderIndex = dto.OrderIndex ?? (i + 1),
                Children = new List<DocumentOutlineItem>()
            };

            var newChildren = BuildNewOutlineItems(dto.Children ?? new List<DocumentOutlineItemPackageDto>(), outlineId, newItemId, level);
            item.Children.AddRange(newChildren);
            result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Recursively adds outline items and their children to the DbContext.
    /// This avoids EF collection tracking issues that can cause concurrency exceptions.
    /// All items are added with completely new IDs to avoid any foreign key conflicts.
    /// </summary>
    private void AddOutlineItemsRecursively(DocumentOutlineItem item)
    {
        // Add the current item to the DbContext
        _db.DocumentOutlineItems.Add(item);
        
        // Recursively add all children
        foreach (var child in item.Children)
        {
            AddOutlineItemsRecursively(child);
        }
    }

    /// <summary>
    /// Adds outline items level by level to ensure parents are always saved before children.
    /// This prevents foreign key constraint violations.
    /// </summary>
    private async Task AddOutlineItemsLevelByLevel(List<DocumentOutlineItem> rootItems)
    {
        var currentLevelItems = rootItems.ToList();
        
        while (currentLevelItems.Count > 0)
        {
            var nextLevelItems = new List<DocumentOutlineItem>();
            
            // Add all items at the current level
            foreach (var item in currentLevelItems)
            {
                // Create a copy of the item without children for saving
                var itemToSave = new DocumentOutlineItem
                {
                    Id = item.Id,
                    SectionNumber = item.SectionNumber,
                    SectionTitle = item.SectionTitle,
                    Level = item.Level,
                    PromptInstructions = item.PromptInstructions,
                    RenderTitleOnly = item.RenderTitleOnly,
                    ParentId = item.ParentId,
                    DocumentOutlineId = item.DocumentOutlineId,
                    OrderIndex = item.OrderIndex,
                    Children = new List<DocumentOutlineItem>() // Empty children collection
                };
                
                _db.DocumentOutlineItems.Add(itemToSave);
                
                // Collect children for the next level
                nextLevelItems.AddRange(item.Children);
            }
            
            // Save the current level before moving to children
            if (currentLevelItems.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
            
            // Move to the next level
            currentLevelItems = nextLevelItems;
        }
    }

    /// <summary>
    /// Returns true if the short name is available for a new process.
    /// </summary>
    [HttpGet("process/check-shortname/{shortName}")]
    public async Task<ActionResult<bool>> IsProcessShortNameAvailable(string shortName)
    {
        shortName = NormalizeShortName(shortName);
        if (string.IsNullOrWhiteSpace(shortName)) { return Ok(false); }
        var existing = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(shortName);
        return Ok(existing == null);
    }

    // ---------- Library export/import ----------

    /// <summary>
    /// Exports a document library definition into a portable package.
    /// </summary>
    [HttpGet("library/{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(DocumentLibraryDefinitionPackageDto))]
    public async Task<ActionResult<DocumentLibraryDefinitionPackageDto>> ExportLibrary(Guid id)
    {
        var libInfo = await _documentLibraryInfoService.GetDocumentLibraryByIdAsync(id);
        if (libInfo == null)
        {
            return NotFound();
        }

        var pkg = BuildLibraryPackageDto(libInfo);
        return Ok(pkg);
    }

    /// <summary>
    /// Imports a document library definition package. New IDs are generated.
    /// </summary>
    [HttpPost("library/import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> ImportLibrary([FromBody] DocumentLibraryDefinitionPackageDto package)
    {
        // Validate package and normalize short name
        var validationResult = ValidateAndNormalizePackage(package);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Check for conflicts
        var conflictResult = await CheckShortNameConflictAsync(package.ShortName!, _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync);
        if (conflictResult != null)
        {
            return conflictResult;
        }

        // Create the library
        var created = await CreateLibraryAsync(package);
        return Ok(created.Id);
    }

    // ---------- Library Helper Methods ----------

    private static DocumentLibraryDefinitionPackageDto BuildLibraryPackageDto(DocumentLibraryInfo libInfo)
    {
        return new DocumentLibraryDefinitionPackageDto
        {
            OriginalLibraryId = libInfo.Id,
            ShortName = libInfo.ShortName,
            DescriptionOfContents = libInfo.DescriptionOfContents,
            DescriptionOfWhenToUse = libInfo.DescriptionOfWhenToUse,
            IndexName = libInfo.IndexName,
            BlobStorageContainerName = libInfo.BlobStorageContainerName,
            BlobStorageAutoImportFolderName = libInfo.BlobStorageAutoImportFolderName,
            LogicType = libInfo.LogicType,
            // Optional chunk settings (if present on DTO)
            VectorStoreChunkSize = (libInfo as dynamic)?.VectorStoreChunkSize,
            VectorStoreChunkOverlap = (libInfo as dynamic)?.VectorStoreChunkOverlap
        };
    }

    private async Task<DocumentLibraryInfo> CreateLibraryAsync(DocumentLibraryDefinitionPackageDto package)
    {
        // Clamp optional chunk values
        package.VectorStoreChunkSize = Clamp(package.VectorStoreChunkSize, 100, 8000);
        package.VectorStoreChunkOverlap = Clamp(package.VectorStoreChunkOverlap, 0, 2000);

        // Default IndexName if not provided
        if (string.IsNullOrWhiteSpace(package.IndexName))
        {
            package.IndexName = $"index-additional-{package.ShortName!.ToLowerInvariant().Replace('.', '-')}";

        }

        // Determine container name that will be used
        var containerName = string.IsNullOrWhiteSpace(package.BlobStorageContainerName)
            ? SanitizeContainerName(package.ShortName!)
            : package.BlobStorageContainerName;

        // Warn if a container with the same name already exists
        if (await _azureFileHelper.ContainerExistsAsync(containerName))
        {
            var msg = $"Blob container '{containerName}' already exists and will be reused.";
            _logger.LogWarning(msg);
            Response.Headers.TryAdd("X-Warning", msg);
        }

        var toCreate = new DocumentLibraryInfo
        {
            ShortName = package.ShortName!,
            DescriptionOfContents = package.DescriptionOfContents,
            DescriptionOfWhenToUse = package.DescriptionOfWhenToUse,
            IndexName = package.IndexName,
            BlobStorageContainerName = containerName,
            BlobStorageAutoImportFolderName = string.IsNullOrWhiteSpace(package.BlobStorageAutoImportFolderName)
                ? "ingest-auto"
                : package.BlobStorageAutoImportFolderName,
            LogicType = package.LogicType,
        };

        // Set optional chunk settings when present on the target DTO (defensive dynamic to avoid compile coupling)
        try
        {
            var dyn = (dynamic)toCreate;
            dyn.VectorStoreChunkSize = package.VectorStoreChunkSize;
            dyn.VectorStoreChunkOverlap = package.VectorStoreChunkOverlap;
        }
        catch
        {
            // ignore if DTO doesn't expose these properties in current version
        }

        return await _documentLibraryInfoService.CreateDocumentLibraryAsync(toCreate);
    }

    /// <summary>
    /// Returns true if the short name is available for a new document library.
    /// </summary>
    [HttpGet("library/check-shortname/{shortName}")]
    public async Task<ActionResult<bool>> IsLibraryShortNameAvailable(string shortName)
    {
        shortName = NormalizeShortName(shortName);
        if (string.IsNullOrWhiteSpace(shortName)) { return Ok(false); }
        var existing = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(shortName);
        return Ok(existing == null);
    }

    /// <summary>
    /// Checks if an index exists and appears to use the SK unified layout by attempting a document lookup
    /// with the expected SkUnifiedRecord format. If this operation fails, it's likely not an SK index.
    /// </summary>
    /// <param name="indexName">The index/collection name to check.</param>
    /// <returns>Compatibility information for the index.</returns>
    [HttpGet("index/compatibility/{indexName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IndexCompatibilityInfoDto>> GetIndexCompatibility(string indexName)
    {
        var info = new IndexCompatibilityInfoDto { IndexName = indexName };
        try
        {
            // First check if the collection exists using the reliable CollectionExistsAsync method
            var exists = await _vectorStoreProvider.CollectionExistsAsync(indexName);
            info.Exists = exists;
            
            if (exists)
            {
                // If it exists, verify it has the SK layout by trying a document partition lookup
                // This should work for SK unified record layouts but may fail for other schemas
                try
                {
                    var partitions = await _vectorStoreProvider.GetDocumentPartitionNumbersAsync(indexName, "__compat_probe__");
                    info.IsSkLayout = true; // If this call succeeds, the schema is compatible with SK unified records
                }
                catch (Exception ex)
                {
                    // Collection exists but schema is incompatible with SK unified records
                    info.IsSkLayout = false;
                    info.Error = $"Collection exists but schema is not compatible: {ex.Message}";
                }
            }
            else
            {
                // Collection doesn't exist
                info.IsSkLayout = false;
            }

            // Dimensions cannot be reliably inferred without data; leave null to indicate unknown
            info.MatchedEmbeddingDimensions = null;
        }
        catch (Exception ex)
        {
            // Unexpected error during existence check
            info.Exists = false;
            info.IsSkLayout = false;
            info.Error = ex.Message;
        }

        return Ok(info);
    }

    // ---------- Shared Utility Methods ----------

    private static string SanitizeContainerName(string name)
    {
        // Azure container naming: lowercase letters, numbers, and hyphens; 3-63 chars; cannot start/end with hyphen.
        var lower = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        lower = lower.Trim('-');
        if (lower.Length < 3)
        {
            lower = (lower + "---").Substring(0, 3);
        }
        if (lower.Length > 63)
        {
            lower = lower.Substring(0, 63).Trim('-');
        }
        if (string.IsNullOrWhiteSpace(lower))
        {
            lower = $"dp-{Guid.NewGuid():N}".Substring(0, 10);
        }
        return lower;
    }

    private static string NormalizeShortName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return string.Empty; }
        // Replace spaces with periods, then remove any char not letter/digit/period
        var replaced = value.Replace(" ", ".");
        var filtered = new string(replaced.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        return filtered.Trim();
    }

    private static int? Clamp(int? value, int min, int max)
    {
        if (value is null) return null;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Creates default implementations for any system prompt definitions that are missing from the imported package.
    /// This ensures imported processes have complete prompt coverage, even if they're from older exports.
    /// Mirrors the logic from DocumentProcessInfoService.CreateMissingPromptImplementations.
    /// </summary>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <param name="processShortName">The short name of the process for logging.</param>
    private async Task CreateMissingPromptImplementationsAsync(Guid documentProcessId, string processShortName)
    {
        // Get all existing prompt implementations for this process
        var existingImplementations = await _db.PromptImplementations
            .Where(pi => pi.DocumentProcessDefinitionId == documentProcessId)
            .Include(pi => pi.PromptDefinition)
            .ToListAsync();

        // Get all system-level prompt definitions
        var allPromptDefinitions = await _db.PromptDefinitions.ToListAsync();

        var defaultImplementationsCreated = 0;

        // Loop through all properties in DefaultPromptCatalogTypes to find missing implementations
        foreach (var promptCatalogProperty in _defaultPromptCatalogTypes.GetType()
                                                                        .GetProperties()
                                                                        .Where(p => p.PropertyType == typeof(string)))
        {
            // Check if this process already has an implementation for this prompt
            var hasImplementation = existingImplementations.Any(pi =>
                pi.PromptDefinition != null && pi.PromptDefinition.ShortCode == promptCatalogProperty.Name);

            if (!hasImplementation)
            {
                // Find the corresponding prompt definition
                var promptDefinition = allPromptDefinitions
                    .FirstOrDefault(pd => pd.ShortCode == promptCatalogProperty.Name);

                if (promptDefinition != null)
                {
                    // Get default text from DefaultPromptCatalogTypes
                    var defaultText = promptCatalogProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? string.Empty;

                    // Create default implementation
                    var defaultImplementation = new PromptImplementation
                    {
                        Id = Guid.NewGuid(),
                        DocumentProcessDefinitionId = documentProcessId,
                        PromptDefinitionId = promptDefinition.Id,
                        Text = defaultText
                    };

                    _db.PromptImplementations.Add(defaultImplementation);
                    defaultImplementationsCreated++;

                    _logger.LogInformation("Created default prompt implementation for '{ShortCode}' in imported process '{ProcessShortName}'", 
                        promptDefinition.ShortCode, processShortName);
                }
                else
                {
                    _logger.LogDebug("Prompt definition '{ShortCode}' not found in system - cannot create default implementation for process '{ProcessShortName}'", 
                        promptCatalogProperty.Name, processShortName);
                }
            }
        }

        if (defaultImplementationsCreated > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Created {Count} default prompt implementations for imported process '{ProcessShortName}'", 
                defaultImplementationsCreated, processShortName);
        }
        else
        {
            _logger.LogDebug("No missing prompt implementations found for imported process '{ProcessShortName}'", processShortName);
        }
    }
}
