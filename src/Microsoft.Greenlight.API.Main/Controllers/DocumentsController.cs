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

namespace Microsoft.Greenlight.API.Main.Controllers;

[Route("/api/documents")]
public class DocumentsController : BaseController
{

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentExporter _wordDocumentExporter;
    private readonly AzureFileHelper _fileHelper;

    public DocumentsController(
        IPublishEndpoint publishEndpoint,
        DocGenerationDbContext dbContext,
        [FromKeyedServices("IDocumentExporter-Word")]
        IDocumentExporter wordDocumentExporter,
        AzureFileHelper fileHelper
        )
    {
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
        _wordDocumentExporter = wordDocumentExporter;
        _fileHelper = fileHelper;
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

    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> IngestDocument([FromBody] DocumentIngestionRequest documentIngestionRequest)
    {
        var claimsPrincipal = HttpContext.User;

        if (documentIngestionRequest.Id == Guid.Empty)
        {
            documentIngestionRequest.Id = Guid.NewGuid();
        }

        documentIngestionRequest.UploadedByUserOid = claimsPrincipal.GetObjectId();
        await _publishEndpoint.Publish<DocumentIngestionRequest>(documentIngestionRequest);

        return Accepted();
    }

    [HttpPost("reindex-all")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReindexAllCompletedDocuments()
    {
        await _publishEndpoint.Publish<ReindexAllCompletedDocuments>(new ReindexAllCompletedDocuments(Guid.NewGuid()));
        return Accepted();
    }

    [HttpGet("{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<GeneratedDocument>]
    public async Task<ActionResult<GeneratedDocument>> GetDocument(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);
        var document = await _dbContext.GeneratedDocuments
            .Include(w => w.ContentNodes)
            .ThenInclude(r => r.Children)
                .ThenInclude(s => s.Children)
                    .ThenInclude(t => t.Children)
                        .ThenInclude(u => u.Children)
                            .ThenInclude(v => v.Children)
                               .ThenInclude(w => w.Children)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == documentGuid);

        if (document == null)
        {
            return NotFound();
        }

        return document;
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

        var accessUrl = _fileHelper.GetProxiedBlobUrl(existingLink.AbsoluteUrl);
        return accessUrl;
    }

    [HttpGet("{documentId}/word-export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> GetDocumentExport(string documentId, string exporterType = "Word")
    {
        var documentResult = await GetDocument(documentId);
        if (documentResult.Result is NotFoundResult)
        {
            return NotFound();
        }

        var document = documentResult.Value;
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
        var documentResult = await GetDocument(documentId);
        if (documentResult.Result is NotFoundResult)
        {
            return NotFound();
        }

        var document = documentResult.Value;
        if (document == null)
        {
            return NotFound();
        }

        var existingLink = await _dbContext.ExportedDocumentLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.GeneratedDocumentId == document.Id && l.MimeType == MimeTypes.MsWordX);

        var blobUri = "";

        if (existingLink is not null)
        {
            blobUri = existingLink.AbsoluteUrl;
        }
        else
        {
            var exportStream = await _wordDocumentExporter.ExportDocumentAsync(document, false);
            var fileNameGuid = Guid.NewGuid();
            var fileName = $"{document.Id}-{fileNameGuid}.docx";
            blobUri = await _fileHelper.UploadFileToBlobAsync(exportStream, fileName, "document-export", true);
            await _fileHelper.SaveFileInfoAsync(blobUri, "document-export", fileName, Guid.Parse(documentId));
        }

        var accessUrl = _fileHelper.GetProxiedBlobUrl(blobUri);
        return accessUrl;
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
            .ToListAsync();

        foreach (var child in children)
        {
            // Recursively delete grandchildren
            await DeleteChildContentNodesRecursively(child);

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
