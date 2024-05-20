using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models.DocumentProcess;
using ProjectVico.V2.Shared.Repositories;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/document-process")]
[Route("/api/document-processes")]
public class DocumentProcessController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly DynamicDocumentProcessDefinitionRepository _repository;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IMapper _mapper;

    public DocumentProcessController(
        DocGenerationDbContext dbContext,
        IDocumentProcessInfoService documentProcessInfoService,
        IMapper mapper, 
        DynamicDocumentProcessDefinitionRepository repository)
    {
        _dbContext = dbContext;
        _documentProcessInfoService = documentProcessInfoService;
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
        var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentInfoListAsync();
        if (documentProcesses.Count <1)
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
        var documentProcess = await _documentProcessInfoService.GetDocumentInfoByShortNameAsync(shortName);
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
        var documentProcess = await _documentProcessInfoService.GetDocumentInfoByIdAsync(id);
        if (documentProcess == null)
        {
            return NotFound();
        }

        return Ok(documentProcess);
    }



    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Produces<DocumentProcessInfo>]
    public async Task<ActionResult<DocumentProcessInfo>> CreateDocumentProcess([FromBody] DocumentProcessInfo documentProcessInfo)
    {
        var dynamicDocumentProcess = _mapper.Map<DynamicDocumentProcessDefinition>(documentProcessInfo);
        
        if (dynamicDocumentProcess.Id == Guid.Empty)
        {
            dynamicDocumentProcess.Id = Guid.NewGuid();
        }

        await _repository.AddAsync(dynamicDocumentProcess);
        await _dbContext.SaveChangesAsync();

        var createdDocumentProcess = await _repository.GetByShortNameAsync(dynamicDocumentProcess.ShortName);

        if (createdDocumentProcess == null)
        {
            return BadRequest();
        }
        
        var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);

        var action = CreatedAtAction(nameof(GetDocumentProcessByShortName), new { shortName = createdDocumentProcessInfo.ShortName }, createdDocumentProcessInfo);
        return action;
    }
    
}