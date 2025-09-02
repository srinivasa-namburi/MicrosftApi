using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Helpers;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing document libraries.
    /// </summary>
    [Route("/api/document-libraries")]
    public class DocumentLibraryController : BaseController
    {
        private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
        private readonly AzureFileHelper _fileHelper;
        private readonly IDocumentIngestionService _documentIngestionService;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly IClusterClient _clusterClient;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentLibraryController"/> class.
        /// </summary>
        /// <param name="documentLibraryInfoService">The document library info service.</param>
        /// <param name="fileHelper">Blob helper for container operations.</param>
        /// <param name="documentIngestionService">Vector store ingestion service for index operations.</param>
        /// <param name="dbContextFactory">DbContext factory for cleanup of ingested rows.</param>
        /// <param name="clusterClient">Orleans cluster client.</param>
        public DocumentLibraryController(
            IDocumentLibraryInfoService documentLibraryInfoService,
            AzureFileHelper fileHelper,
            IDocumentIngestionService documentIngestionService,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            IClusterClient clusterClient
        )
        {
            _documentLibraryInfoService = documentLibraryInfoService;
            _fileHelper = fileHelper;
            _documentIngestionService = documentIngestionService;
            _dbContextFactory = dbContextFactory;
            _clusterClient = clusterClient;
        }

        /// <summary>
        /// Gets all document libraries.
        /// </summary>
        /// <returns>A list of document libraries.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<DocumentLibraryInfo>>]
        public async Task<ActionResult<List<DocumentLibraryInfo>>> GetAllDocumentLibraries()
        {
            var libraries = await _documentLibraryInfoService.GetAllDocumentLibrariesAsync();
            return Ok(libraries);
        }

        /// <summary>
        /// Gets a document library by its ID.
        /// </summary>
        /// <param name="id">The ID of the document library.</param>
        /// <returns>The document library with the specified ID.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        ///     404 Not found: When the document library can't be found using the Id provided
        /// </returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<DocumentLibraryInfo>]
        public async Task<ActionResult<DocumentLibraryInfo>> GetDocumentLibraryById(Guid id)
        {
            var library = await _documentLibraryInfoService.GetDocumentLibraryByIdAsync(id);
            if (library == null)
            {
                return NotFound();
            }
            return Ok(library);
        }

        /// <summary>
        /// Gets a document library by its short name.
        /// </summary>
        /// <param name="shortName">The short name of the document library.</param>
        /// <returns>The document library with the specified short name.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        ///     404 Not found: When the document library can't be found using the Short Name provided
        /// </returns>
        [HttpGet("shortname/{shortName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<DocumentLibraryInfo>]
        public async Task<ActionResult<DocumentLibraryInfo>> GetDocumentLibraryByShortName(string shortName)
        {
            var library = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(shortName);
            if (library == null)
            {
                return NotFound();
            }
            return Ok(library);
        }

        /// <summary>
        /// Gets document libraries by process ID.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>A list of document libraries associated with the specified process ID.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        ///     404 Not found: When the document library info can't be found using the Process Id provided
        /// </returns>
        [HttpGet("by-document-process/{processId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<List<DocumentLibraryInfo>>]
        public async Task<ActionResult<List<DocumentLibraryInfo>>> GetDocumentLibrariesByProcessId(Guid processId)
        {
            var libraries = await _documentLibraryInfoService.GetDocumentLibrariesByProcessIdAsync(processId);
            if (libraries == null || libraries.Count == 0)
            {
                return NotFound();
            }
            return Ok(libraries);
        }

        /// <summary>
        /// Creates a new document library.
        /// </summary>
        /// <param name="documentLibraryInfo">The document library info.</param>
        /// <returns>The created document library.
        /// Produces Status Codes:
        ///     201 Created: When completed sucessfully
        ///     400 Bad Request: When the Document Library Info isn't provided
        /// </returns>
        [HttpPost]
        [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Consumes("application/json")]
        [Produces("application/json")]
        [Produces<DocumentLibraryInfo>]
        public async Task<ActionResult<DocumentLibraryInfo>> CreateDocumentLibrary([FromBody] DocumentLibraryInfo? documentLibraryInfo)
        {
            if (documentLibraryInfo == null)
            {
                return BadRequest("DocumentLibraryInfo cannot be null.");
            }

            var createdLibrary = await _documentLibraryInfoService.CreateDocumentLibraryAsync(documentLibraryInfo);

            return CreatedAtAction(nameof(GetDocumentLibraryById), new { id = createdLibrary.Id }, createdLibrary);
        }

        /// <summary>
        /// Updates an existing document library.
        /// </summary>
        /// <param name="id">The ID of the document library to update.</param>
        /// <param name="documentLibraryInfo">The updated document library info.</param>
        /// <returns>The updated document library.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        ///     400 Bad Request: When the Document Library Info isn't provided or the Id provided doesn't match the Id on the 
        ///     Document Library Inofo object provided
        /// </returns>
        [HttpPut("{id:guid}")]
        [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Consumes("application/json")]
        [Produces("application/json")]
        [Produces<DocumentLibraryInfo>]
        public async Task<ActionResult<DocumentLibraryInfo>> UpdateDocumentLibrary(Guid id, [FromBody] DocumentLibraryInfo? documentLibraryInfo)
        {
            if (documentLibraryInfo == null || id != documentLibraryInfo.Id)
            {
                return BadRequest("Invalid DocumentLibraryInfo.");
            }

            var updatedLibrary = await _documentLibraryInfoService.UpdateDocumentLibraryAsync(documentLibraryInfo);
            return Ok(updatedLibrary);
        }

        /// <summary>
        /// Deletes a document library, including its vector index (if SK) and blob container, and related ingested documents.
        /// </summary>
        /// <param name="id">The ID of the document library to delete.</param>
        /// <returns>No content if the deletion was successful.
        /// Produces Status Codes:
        ///     204 No Content: When completed sucessfully
        ///     404 Not found: When the document library can't be found using the Id provided
        /// </returns>
        [HttpDelete("{id:guid}")]
        [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocumentLibrary(Guid id)
        {
            // Load current library snapshot for cleanup
            var library = await _documentLibraryInfoService.GetDocumentLibraryByIdAsync(id);
            if (library == null)
            {
                return NotFound();
            }

            // First: attempt to deactivate any active ingestion/reindex orchestrations tied to this library
            try
            {
                if (!string.IsNullOrWhiteSpace(library.BlobStorageContainerName) &&
                    !string.IsNullOrWhiteSpace(library.BlobStorageAutoImportFolderName))
                {
                    var ingestionOrchestrationId = IngestionOrchestrationIdHelper.GenerateOrchestrationId(
                        library.BlobStorageContainerName,
                        library.BlobStorageAutoImportFolderName);
                    var ingestionGrain = _clusterClient.GetGrain<IDocumentIngestionOrchestrationGrain>(ingestionOrchestrationId);
                    await ingestionGrain.DeactivateAsync();
                }

                var reindexOrchestrationId = $"library-{library.ShortName}";
                var reindexGrain = _clusterClient.GetGrain<IDocumentReindexOrchestrationGrain>(reindexOrchestrationId);
                await reindexGrain.DeactivateAsync();
            }
            catch (Exception ex)
            {
                // Log and continue with best-effort cleanup
                Console.WriteLine($"Error deactivating orchestrations for library {library.ShortName}: {ex.Message}");
            }

            try
            {
                // 1) Delete vector index/collection for SK Vector Store
                if (library.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore &&
                    !string.IsNullOrWhiteSpace(library.IndexName))
                {
                    await _documentIngestionService.ClearIndexAsync(library.ShortName, library.IndexName);
                }
                // For KernelMemory libraries we currently rely on KM storage lifecycle; TODO: implement explicit index deletion if backend supports it
            }
            catch (Exception ex)
            {
                // Log and continue with best-effort cleanup
                Console.WriteLine($"Error clearing vector index for library {library.ShortName}: {ex.Message}");
            }

            try
            {
                // 2) Delete blob storage container (best-effort)
                if (!string.IsNullOrWhiteSpace(library.BlobStorageContainerName))
                {
                    await _fileHelper.DeleteBlobContainerAsync(library.BlobStorageContainerName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting blob container for library {library.ShortName}: {ex.Message}");
            }

            try
            {
                // 3) Delete tracked ingested documents for this library
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                await db.IngestedDocuments
                    .Where(x => x.DocumentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary
                                && x.DocumentLibraryOrProcessName == library.ShortName)
                    .ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting ingested documents for library {library.ShortName}: {ex.Message}");
            }

            // 4) Remove library record and its associations
            var success = await _documentLibraryInfoService.DeleteDocumentLibraryAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }

        /// <summary>
        /// Associates a document process with a document library.
        /// </summary>
        /// <param name="documentLibraryId">The ID of the document library.</param>
        /// <param name="documentProcessId">The ID of the document process.</param>
        /// <returns>Ok result if the association was successful.
        /// Produces Status Codes:
        ///     204 No Content: When completed sucessfully
        /// </returns>
        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/associate")]
        [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.AssociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return NoContent();
        }

        /// <summary>
        /// Disassociates a document process from a document library.
        /// </summary>
        /// <param name="documentLibraryId">The ID of the document library.</param>
        /// <param name="documentProcessId">The ID of the document process.</param>
        /// <returns>Ok result if the disassociation was successful.
        /// Produces Status Codes:
        ///     204 No Content: When completed sucessfully
        /// </returns>
        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/disassociate")]
        [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DisassociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.DisassociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return NoContent();
        }
    }
}
