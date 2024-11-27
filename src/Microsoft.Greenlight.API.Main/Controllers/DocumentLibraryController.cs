using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    [Route("/api/document-libraries")]
    public class DocumentLibraryController : BaseController
    {
        private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

        public DocumentLibraryController(IDocumentLibraryInfoService documentLibraryInfoService)
        {
            _documentLibraryInfoService = documentLibraryInfoService;
        }

        [HttpGet]
        [Produces(typeof(List<DocumentLibraryInfo>))]
        public async Task<ActionResult<List<DocumentLibraryInfo>>> GetAllDocumentLibraries()
        {
            var libraries = await _documentLibraryInfoService.GetAllDocumentLibrariesAsync();
            return Ok(libraries);
        }

        [HttpGet("{id:guid}")]
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

        [HttpGet("shortname/{shortName}")]
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

        [HttpGet("by-document-process/{processId:guid}")]
        [Produces(typeof(List<DocumentLibraryInfo>))]
        public async Task<ActionResult<List<DocumentLibraryInfo>>> GetDocumentLibrariesByProcessId(Guid processId)
        {
            var libraries = await _documentLibraryInfoService.GetDocumentLibrariesByProcessIdAsync(processId);
            if (libraries == null || !libraries.Any())
            {
                return NotFound();
            }
            return Ok(libraries);
        }

        [HttpPost]
        public async Task<ActionResult<DocumentLibraryInfo>> CreateDocumentLibrary([FromBody] DocumentLibraryInfo? documentLibraryInfo)
        {
            if (documentLibraryInfo == null)
            {
                return BadRequest("DocumentLibraryInfo cannot be null.");
            }

            var createdLibrary = await _documentLibraryInfoService.CreateDocumentLibraryAsync(documentLibraryInfo);
            return CreatedAtAction(nameof(GetDocumentLibraryById), new { id = createdLibrary.Id }, createdLibrary);
        }

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

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteDocumentLibrary(Guid id)
        {
            var success = await _documentLibraryInfoService.DeleteDocumentLibraryAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/associate")]
        public async Task<ActionResult> AssociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.AssociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return Ok();
        }

        [HttpPost("{documentLibraryId:guid}/document-processes/{documentProcessId:guid}/disassociate")]
        public async Task<ActionResult> DisassociateDocumentProcess(Guid documentLibraryId, Guid documentProcessId)
        {
            await _documentLibraryInfoService.DisassociateDocumentProcessAsync(documentLibraryId, documentProcessId);
            return Ok();
        }
    }
}
