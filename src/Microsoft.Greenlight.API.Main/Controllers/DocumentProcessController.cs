using System.Text.Json;
using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services;


namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document processes.
/// </summary>
[Route("/api/document-process")]
[Route("/api/document-processes")]
public class DocumentProcessController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IPluginService _pluginService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="documentProcessInfoService">The document process info service.</param>
    /// <param name="pluginService">The plugin service.</param>
    /// <param name="documentLibraryInfoService">The document library info service.</param>
    /// <param name="mapper">The mapper.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    public DocumentProcessController(
        DocGenerationDbContext dbContext,
        IDocumentProcessInfoService documentProcessInfoService,
        IPluginService pluginService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IMapper mapper,
        IPublishEndpoint publishEndpoint
    )
    {
        _dbContext = dbContext;
        _documentProcessInfoService = documentProcessInfoService;
        _pluginService = pluginService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Gets all document processes.
    /// </summary>
    /// <returns>A list of document processes.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document processes are found
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessInfo>>]
    public async Task<ActionResult<List<DocumentProcessInfo>>> GetAllDocumentProcesses()
    {
        var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        if (documentProcesses.Count < 1)
        {
            return NotFound();
        }

        return Ok(documentProcesses);
    }

    /// <summary>
    /// Gets a document process by short name.
    /// </summary>
    /// <param name="shortName">The short name of the document process.</param>
    /// <returns>The document process info.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document found using the short name provided
    /// </returns>
    [HttpGet("short-name/{shortName}")]
    [HttpGet("{shortName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> GetDocumentProcessByShortName(string shortName)
    {
        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(shortName);
        if (documentProcess == null)
        {
            return NotFound();
        }

        return Ok(documentProcess);
    }

    /// <summary>
    /// Gets a document process by ID.
    /// </summary>
    /// <param name="id">The ID of the document process.</param>
    /// <returns>The document process info.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document processes are found using the Id provided
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> GetDocumentProcessById(Guid id)
    {
        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByIdAsync(id);
        if (documentProcess == null)
        {
            return NotFound();
        }

        return Ok(documentProcess);
    }

    /// <summary>
    /// Gets document processes by library ID.
    /// </summary>
    /// <param name="libraryId">The library ID.</param>
    /// <returns>A list of document processes.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document processes are found using the Library Id provided
    /// </returns>
    [HttpGet("by-document-library/{libraryId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessInfo>>]
    public async Task<ActionResult<List<DocumentProcessInfo>>> GetDocumentProcessesByLibraryId(Guid libraryId)
    {
        var processes = await _documentProcessInfoService.GetDocumentProcessesByLibraryIdAsync(libraryId);
        if (processes == null || processes.Count == 0)
        {
            return NotFound();
        }
        return Ok(processes);
    }

    /// <summary>
    /// Creates a new document process.
    /// </summary>
    /// <param name="documentProcessInfo">The document process info.</param>
    /// <returns>The created document process info.
    /// Produces Status Codes:
    ///     201 Created: When the document process was sucessfully created
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> CreateDocumentProcess([FromBody] DocumentProcessInfo documentProcessInfo)
    {
        var createdDocumentProcessInfo = await _documentProcessInfoService.CreateDocumentProcessInfoAsync(documentProcessInfo);
        // Restarts are done in the background CreateDynamicDocumentProcessPromptsConsumer.
        return Created($"/api/document-process/{createdDocumentProcessInfo.Id}", createdDocumentProcessInfo);
    }

    /// <summary>
    /// Updates an existing document process.
    /// </summary>
    /// <param name="id">The ID of the document process.</param>
    /// <param name="documentProcessInfo">The document process info.</param>
    /// <returns>The updated document process info.
    /// Produces Status Codes:
    ///     202 Accepted: When the update to the Document Process has been made and the Process Restart 
    ///     the Workers has been published
    ///     404 Not Found: When no document processes are found using the Document Process Id provided
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> UpdateDocumentProcess(Guid id, [FromBody] DocumentProcessInfo documentProcessInfo)
    {
        var existingDocumentProcess = await _dbContext.DynamicDocumentProcessDefinitions.FindAsync(id);
        if (existingDocumentProcess == null)
        {
            return NotFound();
        }

        _mapper.Map(documentProcessInfo, existingDocumentProcess);
        _dbContext.DynamicDocumentProcessDefinitions.Update(existingDocumentProcess);
        await _dbContext.SaveChangesAsync();

        if (AdminHelper.IsRunningInProduction())
        {
            await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
        }

        return Accepted($"/api/document-process/{documentProcessInfo.Id}", documentProcessInfo);
    }

    /// <summary>
    /// Deletes a document process.
    /// </summary>
    /// <param name="id">The ID of the document process.</param>
    /// <returns>A boolean indicating success or failure.
    /// Produces Status Codes:
    ///     200 Ok: When the update to the Document Process has been made and the Process Restart 
    ///     the Workers has been published
    ///     400 Bad Request: When no document processes are found using the Document Process Id provided
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<bool>]
    public async Task<ActionResult<bool>> DeleteDocumentProcess(Guid id)
    {
        // Use the Plugin Service to delete all plugin associations for this document process, if any exist
        var plugins = await _pluginService.GetPluginsByDocumentProcessIdAsync(id);
        foreach (var plugin in plugins)
        {
            await _pluginService.DisassociatePluginFromDocumentProcessAsync(plugin.Id, id);
        }

        var documentLibraries = await _documentLibraryInfoService.GetDocumentLibrariesByProcessIdAsync(id);
        foreach (var library in documentLibraries)
        {
            await _documentLibraryInfoService.DisassociateDocumentProcessAsync(library.Id, id);
        }

        // Remove the document process
        var result = await _documentProcessInfoService.DeleteDocumentProcessInfoAsync(id);

        if (AdminHelper.IsRunningInProduction())
        {
            await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
        }
        var resultJson = JsonSerializer.Serialize(result);
        return Ok(resultJson);
    }

    /// <summary>
    /// Exports a document process.
    /// </summary>
    /// <param name="id">The ID of the document process.</param>
    /// <returns>The exported document process as a JSON string.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not found: When no document processes are found using the Id provided
    /// </returns>
    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<string>]
    public async Task<ActionResult<string>> ExportDocumentProcess(Guid id)
    {
        var documentProcessModel = await _dbContext.DynamicDocumentProcessDefinitions
            .Include(x => x.DocumentOutline)
            .Include(x => x.Prompts)
            .ThenInclude(pi => pi.PromptDefinition)
            .ThenInclude(pd => pd != null ? pd.Variables : null)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (documentProcessModel == null)
        {
            return NotFound();
        }

        var promptDefinitions = documentProcessModel.Prompts
            .Where(x => x.PromptDefinition != null)
            .Select(x => x.PromptDefinition!)
            .DistinctBy(x => x.ShortCode)
            .ToList();

        var documentProcessInfo = _mapper.Map<DynamicDocumentProcessDefinition, DocumentProcessInfo>(documentProcessModel);

        var exportModel = new DocumentProcessExportInfo()
        {
            DocumentProcessShortName = documentProcessInfo.ShortName,
            DocumentProcessDescription = documentProcessInfo.Description ?? String.Empty,
            Prompts = JsonSerializer.Serialize(documentProcessModel.Prompts),
            PromptDefinitions = JsonSerializer.Serialize(promptDefinitions)
        };

        var exportJson = JsonSerializer.Serialize(exportModel);
        return Ok(exportJson);
    }

    /// <summary>
    /// Retrieves the metadata fields for a document process.
    /// </summary>
    /// <param name="id">Document Process ID</param>
    /// <returns>A list of metadata fields.
    /// Produces Status Codes:
    ///   200 OK: When completed successfully
    ///   404 Not found: When no metadata fields are found using the Id provided
    /// </returns>
    [HttpGet("{id:guid}/metadata-fields")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessMetadataFieldInfo>>]
    public async Task<ActionResult<List<DocumentProcessMetadataFieldInfo>>> GetDocumentProcessMetadataFields(Guid id)
    {
        var metadataFields = await _dbContext.DynamicDocumentProcessMetaDataFields.Where(x=>x.DynamicDocumentProcessDefinitionId == id)
            .ToListAsync();

        if (metadataFields.Count < 1)
        {
            return NotFound();
        }

        var metaDataInfoFields = _mapper.Map<
            List<DynamicDocumentProcessMetaDataField>, 
            List<DocumentProcessMetadataFieldInfo>>
            (metadataFields);
        
        return Ok(metaDataInfoFields);
    }

    /// <summary>
    /// Stores metadata fields for a document process.
    /// </summary>
    /// <param name="id">Document Process ID</param>
    /// <param name="metadataFields">List of metadata fields to store</param>
    /// <returns>A list of metadata fields.
    /// Produces Status Codes:
    ///   201 Created: When the metadata fields have been successfully created or updated
    ///   400 Bad Request: When no metadata fields were passed in the request or the Id is empty
    /// </returns>
    [HttpPost("{id:guid}/metadata-fields")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<List<DocumentProcessMetadataFieldInfo>>]
    public async Task<ActionResult<List<DocumentProcessMetadataFieldInfo>>> CreateOrUpdateDocumentProcessMetadataFields(Guid id, [FromBody] List<DocumentProcessMetadataFieldInfo> metadataFields)
    {
        if (metadataFields.Count < 1 || id == Guid.Empty)
        {
            return BadRequest();
        }

        var existingMetadataFields = await _dbContext.DynamicDocumentProcessMetaDataFields.Where(x => x.DynamicDocumentProcessDefinitionId == id)
            .ToListAsync();

        if (existingMetadataFields.Count > 0)
        {
            _dbContext.DynamicDocumentProcessMetaDataFields.RemoveRange(existingMetadataFields);
            await _dbContext.SaveChangesAsync();
        }

        foreach (var field in metadataFields)
        {
            field.DynamicDocumentProcessDefinitionId = id;
            if (field.Id == Guid.Empty)
            {
                field.Id = Guid.NewGuid();
            }
        }

        var newMetadataFields = _mapper.Map<List<DocumentProcessMetadataFieldInfo>, List<DynamicDocumentProcessMetaDataField>>(metadataFields);
        foreach (var field in newMetadataFields)
        {
            field.DynamicDocumentProcessDefinitionId = id;
        }

        await _dbContext.DynamicDocumentProcessMetaDataFields.AddRangeAsync(newMetadataFields);
        await _dbContext.SaveChangesAsync();
        return Created($"/api/document-process/{id}/metadata-fields", metadataFields);
    }
}