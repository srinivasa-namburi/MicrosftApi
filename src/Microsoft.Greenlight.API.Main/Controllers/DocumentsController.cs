using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.API.Main.Controllers;

[Route("/api/documents")]
public class DocumentsController : BaseController
{

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentExporter _wordDocumentExporter;
    private readonly AzureFileHelper _fileHelper;
    private readonly IMapper _mapper;

    public DocumentsController(
        IPublishEndpoint publishEndpoint,
        DocGenerationDbContext dbContext,
        [FromKeyedServices("IDocumentExporter-Word")]
        IDocumentExporter wordDocumentExporter,
        AzureFileHelper fileHelper, IMapper mapper)
    {
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
        _wordDocumentExporter = wordDocumentExporter;
        _fileHelper = fileHelper;
        _mapper = mapper;
    }

    [HttpPost("generatemultiple")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateDocuments([FromBody] GenerateDocumentsDTO generateDocumentsDto)
    {
        var claimsPrincipal = HttpContext.User;

        foreach (var generateDocumentDto in generateDocumentsDto.Documents)
        {
            generateDocumentDto.AuthorOid = claimsPrincipal.GetObjectId();
            await _publishEndpoint.Publish<GenerateDocumentDTO>(generateDocumentDto);
        }

        return Accepted();
    }

    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateDocumentDTO generateDocumentDto)
    {
        var claimsPrincipal = HttpContext.User;
        generateDocumentDto.AuthorOid = claimsPrincipal.GetObjectId();

        await _publishEndpoint.Publish<GenerateDocumentDTO>(generateDocumentDto);

        return Accepted();
    }

    [HttpGet("{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<GeneratedDocumentInfo>]
    public async Task<ActionResult<GeneratedDocumentInfo>> GetFullGeneratedDocument(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);

        // Load the GeneratedDocument
        var document = await AssembleFullDocument(documentGuid);

        var documentInfo = _mapper.Map<GeneratedDocumentInfo>(document);

        return Ok(documentInfo);
    }

    private async Task<GeneratedDocument?> AssembleFullDocument(Guid documentGuid)
    {
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentGuid);

        if (document == null)
        {
            return document;
        }

        // Step 1: Load top-level ContentNodes
        var topLevelNodes = await _dbContext.ContentNodes
            .AsNoTracking()
            .Include(cn => cn.ContentNodeSystemItem)
            .Where(cn => cn.GeneratedDocumentId == documentGuid)
            .ToListAsync();

        // Step 2: Load all descendants
        var descendantNodes = await GetAllDescendantContentNodesAsync(topLevelNodes.Select(cn => cn.Id).ToList());

        // Combine all ContentNodes
        var allContentNodes = topLevelNodes.Concat(descendantNodes).ToList();

        // Build the hierarchy
        var contentNodeDict = allContentNodes.ToDictionary(cn => cn.Id);

        // Initialize Children collections
        foreach (var node in allContentNodes)
        {
            node.Children = new List<ContentNode>();
        }

        // Link parents and children
        foreach (var node in allContentNodes)
        {
            if (node.ParentId.HasValue && contentNodeDict.TryGetValue(node.ParentId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
        }

        // Assign the root ContentNodes to the document
        document.ContentNodes = topLevelNodes;
        return document;
    }

    private async Task<List<ContentNode>> GetAllDescendantContentNodesAsync(List<Guid> parentIds)
    {
        var allDescendants = new List<ContentNode>();
        var currentLevelIds = parentIds;

        while (currentLevelIds.Any())
        {
            // Load the children of the current level
            var childNodes = await _dbContext.ContentNodes
                .AsNoTracking()
                .Include(cn => cn.ContentNodeSystemItem)
                .Where(cn => cn.ParentId.HasValue && currentLevelIds.Contains(cn.ParentId.Value))
                .ToListAsync();

            if (!childNodes.Any())
            {
                break;
            }

            allDescendants.AddRange(childNodes);

            // Prepare for the next level
            currentLevelIds = childNodes.Select(cn => cn.Id).ToList();
        }

        return allDescendants;
    }

    [HttpGet("{documentId}/export-link")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<string>]
    public async Task<ActionResult<string>> GetPreparedDocumentLink(string documentId)
    {
        var existingLink = await _dbContext.ExportedDocumentLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.GeneratedDocumentId == new Guid(documentId) && l.MimeType == MimeTypes.MsWordX);

        if (existingLink is null)
        {
            return NotFound();
        }

        var assetUrl = _fileHelper.GetProxiedAssetBlobUrl(existingLink.Id);
        return assetUrl;
    }

    [HttpGet("{documentId}/word-export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> GetDocumentExportFile(string documentId, string exporterType = "Word")
    {
        var document = await AssembleFullDocument(Guid.Parse(documentId));
        if (document == null)
        {
            return NotFound();
        }
        
        IDocumentExporter exporter;

        // Capitalize the first letter of the exporter type, lowercase the rest
        exporterType = exporterType.First().ToString().ToUpper() + exporterType.Substring(1).ToLower();

        switch (exporterType)
        {
            case "Word":
                exporter = _wordDocumentExporter;
                break;
            default:
                return BadRequest();
        }

        var titleNumberingRegex = new Regex(IDocumentExporter.TitleNumberingRegex);

        var documentGuid = Guid.Parse(documentId);
        var documentHasNumbering = _dbContext.ContentNodes
            .AsNoTracking()
            .AsSplitQuery()
            .Where(cn => cn.GeneratedDocumentId == documentGuid && cn.Type != Shared.Enums.ContentNodeType.BodyText)
            .Join(_dbContext.ContentNodes.Where(cn => cn.Type != Shared.Enums.ContentNodeType.BodyText), cn1 => cn1.Id, cn2 => cn2.ParentId, (cn1, cn2) => cn2)
            .ToList()
            .All(cn => titleNumberingRegex.IsMatch(cn.Text));

        var exportStream = await exporter.ExportDocumentAsync(document, documentHasNumbering);


        return File(exportStream, "application/octet-stream", $"{document.Title}.docx");
    }

    [HttpGet("{documentId}/word-export/permalink")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<string>> GetDocumentExportPermalink(string documentId)
    {
        var document = await AssembleFullDocument(Guid.Parse(documentId));
        
        if (document == null)
        {
            return NotFound();
        }

        var documentExportedLink = await _dbContext.ExportedDocumentLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.GeneratedDocumentId == document.Id && l.MimeType == MimeTypes.MsWordX);

        var blobUri = "";

        if (documentExportedLink is not null)
        {
            // The document has already been exported/generated - return the existing link
            blobUri = documentExportedLink.AbsoluteUrl;
        }
        else
        {
            // The document has not been exported/generated - generate and export it now
            var exportStream = await _wordDocumentExporter.ExportDocumentAsync(document, false);
            var fileNameGuid = Guid.NewGuid();
            var fileName = $"{document.Id}-{fileNameGuid}.docx";
            blobUri = await _fileHelper.UploadFileToBlobAsync(exportStream, fileName, "document-export", true);
            documentExportedLink = await _fileHelper.SaveFileInfoAsync(blobUri, "document-export", fileName, Guid.Parse(documentId));
        }

        var assetUrl = _fileHelper.GetProxiedAssetBlobUrl(documentExportedLink.Id);
        return assetUrl;
        //var accessUrl = _fileHelper.GetProxiedBlobUrl(blobUri);
        //return accessUrl;
    }

    [HttpDelete("{documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);
        var document = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(d => d.Id == documentGuid);

        if (document == null)
        {
            return NotFound();
        }
        var contentNodes = await _dbContext.ContentNodes
            .Where(c => c.GeneratedDocumentId == documentGuid)
            .ToListAsync();

        foreach (var contentNode in contentNodes)
        {
            // Recursively delete children
            await DeleteChildContentNodesRecursively(contentNode);

            // Delete the top-level ContentNode after its children are removed
            _dbContext.ContentNodes.Remove(contentNode);
        }

        var documentMetaData = await _dbContext.DocumentMetadata
            .FirstOrDefaultAsync(m => m.GeneratedDocumentId == documentGuid);

        if (documentMetaData != null)
        {
            _dbContext.DocumentMetadata.Remove(documentMetaData);
        }

        _dbContext.GeneratedDocuments.Remove(document);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private async Task DeleteChildContentNodesRecursively(ContentNode parentNode)
    {
        // Load children of the current node
        var children = await _dbContext.ContentNodes
            .Where(c => c.ParentId == parentNode.Id)
            .Include(x=>x.ContentNodeSystemItem)
            .ThenInclude(x=>x!.SourceReferences)
            .ToListAsync();

        foreach (var child in children)
        {
            // Recursively delete grandchildren
            await DeleteChildContentNodesRecursively(child);
            
            if (child.ContentNodeSystemItem != null)
            {
                foreach (var sourceReference in child.ContentNodeSystemItem.SourceReferences)
                {
                    _dbContext.SourceReferenceItems.Remove(sourceReference);
                }
                _dbContext.ContentNodeSystemItems.Remove(child.ContentNodeSystemItem);
            }

            // Delete the child node after its children have been deleted
            _dbContext.ContentNodes.Remove(child);
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<List<GeneratedDocumentListItem>>]
    public async Task<ActionResult<List<GeneratedDocumentListItem>>?> GetGeneratedDocuments()
    {
        var documents = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .Select(d => new GeneratedDocumentListItem
            {
                Id = d.Id,
                Title = d.Title,
                GeneratedDate = d.GeneratedDate,
                RequestingAuthorOid = d.RequestingAuthorOid
            })
            .ToListAsync();

        if (documents == null)
        {
            return NotFound();
        }

        return documents;
    }
}
