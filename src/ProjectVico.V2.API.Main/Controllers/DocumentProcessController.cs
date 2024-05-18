using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Services.DocumentInfo;

namespace ProjectVico.V2.API.Main.Controllers;

[Route("/api/document-process")]
[Route("/api/document-processes")]
public class DocumentProcessController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IMapper _mapper;

    public DocumentProcessController(
        DocGenerationDbContext dbContext,
        IDocumentProcessInfoService documentProcessInfoService,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _documentProcessInfoService = documentProcessInfoService;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessInfo>>]
    public async Task<ActionResult<List<DocumentProcessInfo>>> GetAllDocumentProcesses()
    {
        var documentProcesses = _documentProcessInfoService.GetCombinedDocumentInfoList();
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

        var createdDocumentProcess = await _dbContext.DynamicDocumentProcessDefinitions.AddAsync(dynamicDocumentProcess);
        await _dbContext.SaveChangesAsync();

        if (createdDocumentProcess.Entity == null)
        {
            return BadRequest();
        }
        
        var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);

        var action = CreatedAtAction(nameof(GetDocumentProcessByShortName), new { shortName = createdDocumentProcessInfo.ShortName }, createdDocumentProcessInfo);
        return action;
    }
    
}