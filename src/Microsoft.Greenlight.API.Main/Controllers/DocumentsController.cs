
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing document-related operations.
/// </summary>
[Route("/api/documents")]
public partial class DocumentsController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IDocumentExporter _wordDocumentExporter;
    private readonly IContentNodeService _contentNodeService;
    private readonly AzureFileHelper _fileHelper;
    private readonly IMapper _mapper;
    private readonly IClusterClient _clusterClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentsController"/> class.
    /// </summary>
    /// <param name="publishEndpoint">The publish endpoint for sending messages.</param>
    /// <param name="dbContext">The database context for document generation.</param>
    /// <param name="wordDocumentExporter">The document exporter for Word documents.</param>
    /// <param name="contentNodeService">The content node service for retrieving and sorting Content Nodes</param>
    /// <param name="fileHelper">The Azure file helper for managing files.</param>
    /// <param name="mapper">The AutoMapper instance for mapping objects.</param>
    /// <param name="clusterClient"></param>
    public DocumentsController(
        DocGenerationDbContext dbContext,
        [FromKeyedServices("IDocumentExporter-Word")]
        IDocumentExporter wordDocumentExporter,
        IContentNodeService contentNodeService,
        AzureFileHelper fileHelper,
        IMapper mapper, 
        IClusterClient clusterClient)
    {
        _dbContext = dbContext;
        _wordDocumentExporter = wordDocumentExporter;
        _contentNodeService = contentNodeService;
        _fileHelper = fileHelper;
        _mapper = mapper;
        _clusterClient = clusterClient;
    }

    /// <summary>
    /// Generates multiple documents based on the provided DTO.
    /// </summary>
    /// <param name="generateDocumentsDto">The DTO containing the documents to generate.</param>
    /// <returns>An accepted result if the documents are being generated.
    /// Produces Status Codes:
    ///     202 Accepted: When the request to generate documents has been posted to the workers to perform
    /// </returns>
    [HttpPost("generatemultiple")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateDocuments([FromBody] GenerateDocumentsDTO generateDocumentsDto)
    {
        var claimsPrincipal = HttpContext.User;

        foreach (var generateDocumentDto in generateDocumentsDto.Documents)
        {
            generateDocumentDto.AuthorOid = claimsPrincipal.GetObjectId();
            var grain = _clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(generateDocumentDto.Id);
            // Fire and forget the document creation process - this progresses
            // asynchronously and will be tracked by the orchestration grain
            _ = grain.StartDocumentGenerationAsync(generateDocumentDto);
        }

        return Accepted();
    }

    /// <summary>
    /// Generates a single document based on the provided DTO.
    /// </summary>
    /// <param name="generateDocumentDto">The DTO containing the document to generate.</param>
    /// <returns>An accepted result if the document is being generated.
    /// Produces Status Codes:
    ///     202 Accepted: When the request to generate documents has been posted to the workers to perform
    /// </returns>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateDocumentDTO generateDocumentDto)
    {
        var claimsPrincipal = HttpContext.User;
        generateDocumentDto.AuthorOid = claimsPrincipal.GetObjectId();

        var grain = _clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(generateDocumentDto.Id);

        // Fire and forget the document creation process - this progresses 
        // asynchronously and will be tracked by the orchestration grain
        _ = grain.StartDocumentGenerationAsync(generateDocumentDto);
        
        return Accepted();
    }

    /// <summary>
    /// Gets the full generated document by its ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to retrieve.</param>
    /// <returns>The full generated document information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When no document can be found using the document id provided
    /// </returns>
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

        if (document == null)
        {
            return NotFound();
        }

        var documentInfo = _mapper.Map<GeneratedDocumentInfo>(document);

        return Ok(documentInfo);
    }

    /// <summary>
    /// Gets the header information of the generated document by its ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to retrieve.</param>
    /// <returns>The header information for the generated document.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When no document can be found using the document id provided
    /// </returns>
    [HttpGet("{documentId}/header")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<GeneratedDocumentInfo>]
    public async Task<ActionResult<GeneratedDocumentInfo>> GetGeneratedDocumentHeader(string documentId)
    {
        var documentGuid = Guid.Parse(documentId);
        // Load the GeneratedDocument
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentGuid);
        if (document == null)
        {
            return NotFound();
        }
        var documentInfo = _mapper.Map<GeneratedDocumentInfo>(document);
        return Ok(documentInfo);
    }

    /// <summary>
    /// Assembles the full document including its content nodes.
    /// </summary>
    /// <param name="documentGuid">The GUID of the document to assemble.</param>
    /// <returns>The assembled document.</returns>
    private async Task<GeneratedDocument?> AssembleFullDocument(Guid documentGuid)
    {
        var document = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentGuid);

        if (document == null)
        {
            return document;
        }

        // Fetch Content Nodes from the Content Node Service
        var contentNodes = await _contentNodeService.GetContentNodesHierarchicalAsyncForDocumentId(documentGuid);

        if (contentNodes == null)
        {
            return null;
        }

        document.ContentNodes = contentNodes;


        return document;
    }

    /// <summary>
    /// Gets the prepared document link for the given document ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to get the link for.</param>
    /// <returns>The prepared document link.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When no document links can be found using the document id provided
    /// </returns>
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

    /// <summary>
    /// Gets the document export file for the given document ID and exporter type.
    /// </summary>
    /// <param name="documentId">The ID of the document to export.</param>
    /// <param name="exporterType">The type of exporter to use (default is "Word").</param>
    /// <returns>The exported document file.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the document can't be found using the document id provided
    /// </returns>
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
        exporterType = exporterType.First().ToString().ToUpper() + exporterType[1..].ToLower();

        switch (exporterType)
        {
            case "Word":
                exporter = _wordDocumentExporter;
                break;
            default:
                return BadRequest();
        }

        var titleNumberingRegex = TitleNumberingRegexPattern();

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

    [GeneratedRegex(IDocumentExporter.TitleNumberingRegex)]
    private static partial Regex TitleNumberingRegexPattern();

    /// <summary>
    /// Gets the permalink for the document export for the given document ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to get the permalink for.</param>
    /// <returns>The permalink for the document export.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the document can't be found using the Id provided
    /// </returns>
    [HttpGet("{documentId}/word-export/permalink")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<string>]
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
    }

    /// <summary>
    /// Deletes the document with the given ID.
    /// </summary>
    /// <param name="documentId">The ID of the document to delete.</param>
    /// <returns>No content if the document was deleted successfully.
    /// Produces Status Codes:
    ///     204 No Content: When completed sucessfully
    ///     404 Not Found: When the document can't be found using the Id provided
    /// </returns>
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
            await DeleteChildContentNodesRecursivelyAsync(contentNode);

            // Delete the top-level ContentNode after its children are removed
            _dbContext.ContentNodes.Remove(contentNode);
        }

        var validationPipelineExecutions = await _dbContext.ValidationPipelineExecutions
            .Where(v => v.GeneratedDocumentId == documentGuid)
            .ToListAsync();

        foreach (var validationPipelineExecution in validationPipelineExecutions)
        {
            await DeleteValidationPipelineExecutionAsync(validationPipelineExecution);
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

    /// <summary>
    /// Recursively deletes child content nodes for the given parent node.
    /// </summary>
    /// <param name="parentNode">The parent node to delete children for.</param>
    private async Task DeleteChildContentNodesRecursivelyAsync(ContentNode parentNode)
    {
        // Load children of the current node
        var children = await _dbContext.ContentNodes
            .Where(c => c.ParentId == parentNode.Id)
            .Include(x => x.ContentNodeSystemItem)
            .ThenInclude(x => x!.SourceReferences)
            .ToListAsync();

        foreach (var child in children)
        {
            // Recursively delete grandchildren
            await DeleteChildContentNodesRecursivelyAsync(child);

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

    /// <summary>
    /// Deletes a validation pipeline execution and its related entities.
    /// </summary>
    /// <param name="validationPipelineExecution">The validation pipeline execution to delete.</param>
    private async Task DeleteValidationPipelineExecutionAsync(ValidationPipelineExecution validationPipelineExecution)
    {
        var executionSteps = await _dbContext.ValidationPipelineExecutionSteps
            .Where(step => step.ValidationPipelineExecutionId == validationPipelineExecution.Id)
            .ToListAsync();

        foreach (var step in executionSteps)
        {
            var stepResults = await _dbContext.ValidationPipelineExecutionStepResults
                .Where(result => result.ValidationPipelineExecutionStepId == step.Id)
                .ToListAsync();

            foreach (var result in stepResults)
            {
                var contentNodeResults = await _dbContext.ValidationExecutionStepContentNodeResults
                    .Where(cnr => cnr.ValidationPipelineExecutionStepResultId == result.Id)
                    .ToListAsync();

                foreach (var contentNodeResult in contentNodeResults)
                {
                    // If the validation step resulted in a change (and thus a new content node), delete the new content node
                    // as this is an orphaned node (validate with AssociatedGeneratedDocumentId or GeneratedDocumentId)
                    if (contentNodeResult.ResultantContentNodeId != contentNodeResult.OriginalContentNodeId)
                    {
                        var newContentNode = await _dbContext.ContentNodes
                            .FirstOrDefaultAsync(cn => cn.Id == contentNodeResult.ResultantContentNodeId);

                        if (newContentNode == null)
                        {
                            continue;
                        }

                        contentNodeResult.ResultantContentNodeId = contentNodeResult.OriginalContentNodeId;
                        
                        _dbContext.Update(contentNodeResult);
                        _dbContext.ContentNodes.Remove(newContentNode);
                    }

                    _dbContext.ValidationExecutionStepContentNodeResults.Remove(contentNodeResult);
                }

                _dbContext.ValidationPipelineExecutionStepResults.Remove(result);
            }

            _dbContext.ValidationPipelineExecutionSteps.Remove(step);
        }

        _dbContext.ValidationPipelineExecutions.Remove(validationPipelineExecution);
    }

    /// <summary>
    /// Gets the list of generated documents.
    /// </summary>
    /// <returns>The list of generated documents.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When there are documents found
    /// </returns>
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
