using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing document libraries.
    /// </summary>
    [Route("/api/document-libraries")]
    public class DocumentLibraryController : BaseController
    {
        private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentLibraryController"/> class.
        /// </summary>
        /// <param name="documentLibraryInfoService">The document library info service.</param>
        /// <param name="publishEndpoint">The publish endpoint.</param>
        public DocumentLibraryController(
            IDocumentLibraryInfoService documentLibraryInfoService,
            IPublishEndpoint publishEndpoint
        )
        {
            _documentLibraryInfoService = documentLibraryInfoService;
            _publishEndpoint = publishEndpoint;
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
        [Produces(typeof(List<DocumentLibraryInfo>))]
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
        [Produces(typeof(DocumentLibraryInfo))]
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
        [Produces(typeof(DocumentLibraryInfo))]
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
        [Produces(typeof(List<DocumentLibraryInfo>))]
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
        ///     200 OK: When completed sucessfully
        ///     400 Bad Request: When the Document Library Info isn't provided
        /// </returns>
        [HttpPost]
        public async Task<ActionResult<DocumentLibraryInfo>> CreateDocumentLibrary([FromBody] DocumentLibraryInfo? documentLibraryInfo)
        {
            if (documentLibraryInfo == null)
            {
                return BadRequest("DocumentLibraryInfo cannot be null.");
            }

            var createdLibrary = await _documentLibraryInfoService.CreateDocumentLibraryAsync(documentLibraryInfo);

            if (AdminHelper.IsRunningInProduction())
            {
                await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
            }

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
        /// Deletes a document library.
        /// </summary>
        /// <param name="id">The ID of the document library to delete.</param>
        /// <returns>No content if the deletion was successful.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        ///     404 Not found: When the document library can't be found using the Id provided
        /// </returns>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteDocumentLibrary(Guid id)
        {
            var success = await _documentLibraryInfoService.DeleteDocumentLibraryAsync(id);
            if (!success)
            {
                return NotFound();
            }

            if (AdminHelper.IsRunningInProduction())
            {
                await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
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
        ///     200 OK: When completed sucessfully
        /// </returns>
        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/associate")]
        public async Task<ActionResult> AssociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.AssociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return Ok();
        }

        /// <summary>
        /// Disassociates a document process from a document library.
        /// </summary>
        /// <param name="documentLibraryId">The ID of the document library.</param>
        /// <param name="documentProcessId">The ID of the document process.</param>
        /// <returns>Ok result if the disassociation was successful.
        /// Produces Status Codes:
        ///     200 OK: When completed sucessfully
        /// </returns>
        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/disassociate")]
        public async Task<ActionResult> DisassociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.DisassociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return Ok();
        }
    }
}
