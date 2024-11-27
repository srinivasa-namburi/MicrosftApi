using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;
using Microsoft.Greenlight.Shared.Services;
using MongoDB.Libmongocrypt;

namespace Microsoft.Greenlight.API.Main.Controllers;

[Route("/api/document-process")]
[Route("/api/document-processes")]
public class DocumentProcessController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly DynamicDocumentProcessDefinitionRepository _repository;

    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IPluginService _pluginService;
    private readonly IMapper _mapper;

    public DocumentProcessController(
        DocGenerationDbContext dbContext,
        IDocumentProcessInfoService documentProcessInfoService,
        IPluginService pluginService,
        IMapper mapper,
        DynamicDocumentProcessDefinitionRepository repository)
    {
        _dbContext = dbContext;
        _documentProcessInfoService = documentProcessInfoService;
        _pluginService = pluginService;
        _mapper = mapper;
        _repository = repository;
    }

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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    [HttpGet("by-document-library/{libraryId:guid}")]
    [Produces(typeof(List<DocumentProcessInfo>))]
    public async Task<ActionResult<List<DocumentProcessInfo>>> GetDocumentProcessesByLibraryId(Guid libraryId)
    {
        var processes = await _documentProcessInfoService.GetDocumentProcessesByLibraryIdAsync(libraryId);
        if (processes == null || !processes.Any())
        {
            return NotFound();
        }
        return Ok(processes);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> CreateDocumentProcess([FromBody] DocumentProcessInfo documentProcessInfo)
    {
        var createdDocumentProcessInfo = await _documentProcessInfoService.CreateDocumentProcessInfoAsync(documentProcessInfo);

        return Created($"/api/document-process/{createdDocumentProcessInfo.Id}", createdDocumentProcessInfo);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> UpdateDocumentProcess(Guid id, [FromBody] DocumentProcessInfo documentProcessInfo)
    {
        var existingDocumentProcess = await _dbContext.DynamicDocumentProcessDefinitions.FindAsync(id);
        if (existingDocumentProcess == null)
        {
            return BadRequest();
        }

        _mapper.Map(documentProcessInfo, existingDocumentProcess);
        _dbContext.DynamicDocumentProcessDefinitions.Update(existingDocumentProcess);
        await _dbContext.SaveChangesAsync();
        
        return Accepted($"/api/document-process/{documentProcessInfo.Id}", documentProcessInfo);
    }

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

        // Remove the document process
        var result = await _documentProcessInfoService.DeleteDocumentProcessInfoAsync(id);
        var resultJson = JsonSerializer.Serialize(result);
        return Ok(resultJson);
    }

    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<string>> ExportDocumentProcess(Guid id)
    {

        var documentProcessModel = await _dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .Include(x => x.Prompts)
                .ThenInclude(pi => pi.PromptDefinition)
                .ThenInclude(pd => pd.Variables)
                .FirstOrDefaultAsync(x => x.Id == id)
            ;

        var promptDefinitions = documentProcessModel.Prompts.Select(x => x.PromptDefinition).DistinctBy(x=>x.ShortCode).ToList();

        if (documentProcessModel == null)
        {
            return NotFound();
        }

        var documentProcessInfo = _mapper.Map<DynamicDocumentProcessDefinition, DocumentProcessInfo>(documentProcessModel);

        var exportModel = new DocumentProcessExportInfo()
        {
            DocumentProcessShortName = documentProcessInfo.ShortName,
            DocumentProcessDescription = documentProcessInfo.Description,
            Prompts = JsonSerializer.Serialize(documentProcessModel.Prompts),
            PromptDefinitions = JsonSerializer.Serialize(promptDefinitions)
        };

        var exportJson = JsonSerializer.Serialize(exportModel);
        return Ok(exportJson);

    }
}
