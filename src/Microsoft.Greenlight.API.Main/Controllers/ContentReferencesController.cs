using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services.ContentReference;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing content references.
    /// </summary>
    [Route("/api/content-references")]
    public class ContentReferencesController : BaseController
    {
        private readonly IContentReferenceService _contentReferenceService;
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<ContentReferencesController> _logger;
        private readonly DocGenerationDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentReferencesController"/> class.
        /// </summary>
        /// <param name="contentReferenceService">The content reference service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="dbContext">The database context</param>
        /// <param name="clusterClient">Orleans Cluster Client</param>
        public ContentReferencesController(
            IContentReferenceService contentReferenceService,
            ILogger<ContentReferencesController> logger, 
            DocGenerationDbContext dbContext, 
            IClusterClient clusterClient)
        {
            _contentReferenceService = contentReferenceService;
            _logger = logger;
            _dbContext = dbContext;
            _clusterClient = clusterClient;
        }

        /// <summary>
        /// Gets all content references.
        /// </summary>
        /// <returns>A list of all content references.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     500 Internal Server Error: When an error occurs while retrieving references
        /// </returns>
        [HttpGet("all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<List<ContentReferenceItemInfo>>> GetAllReferences()
        {
            try
            {
                var references = await _contentReferenceService.GetAllCachedReferencesAsync();
                return Ok(references);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all references");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving references");
            }
        }

        /// <summary>
        /// Searches for content references by term.
        /// </summary>
        /// <param name="term">The search term.</param>
        /// <returns>A list of matching content references.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     500 Internal Server Error: When an error occurs while searching references
        /// </returns>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<List<ContentReferenceItemInfo>>> SearchReferences(string term = "")
        {
            try
            {
                var references = await _contentReferenceService.SearchCachedReferencesAsync(term);
                return Ok(references);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching references");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error searching references");
            }
        }

        /// <summary>
        /// Gets a content reference by its ID and type.
        /// </summary>
        /// <param name="id">The ID of the content reference.</param>
        /// <param name="type">The type of the content reference.</param>
        /// <returns>The content reference item.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     404 Not Found: When the content reference is not found
        ///     500 Internal Server Error: When an error occurs while retrieving the reference
        /// </returns>
        [HttpGet("{id}/{type}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<ContentReferenceItemInfo>> GetReferenceById(Guid id, ContentReferenceType type)
        {
            try
            {
                var reference = await _contentReferenceService.GetCachedReferenceByIdAsync(id, type);
                if (reference == null)
                    return NotFound();

                return Ok(reference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reference by ID");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving reference");
            }
        }

        /// <summary>
        /// Refreshes the content references cache.
        /// </summary>
        /// <returns>A confirmation message.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     500 Internal Server Error: When an error occurs while refreshing the cache
        /// </returns>
        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult> RefreshReferenceCache()
        {
            try
            {
                await _contentReferenceService.RefreshReferencesCacheAsync();
                return Ok("Reference cache refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing reference cache");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error refreshing reference cache");
            }
        }

        /// <summary>
        /// Removes a content reference from a conversation.
        /// </summary>
        /// <param name="referenceId">The ID of the reference to remove.</param>
        /// <param name="conversationId">The conversation from which to remove the reference.</param>
        /// <returns>A confirmation message.</returns>
        [HttpDelete("remove/{referenceId}/{conversationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveReference(Guid referenceId, Guid conversationId)
        {
            try
            {
                var conversationGrain = _clusterClient.GetGrain<IConversationGrain>(conversationId);

                if (conversationGrain == null)
                {
                    return NotFound("Conversation not found");
                }
                var conversationState = await conversationGrain.GetStateAsync();
                // Remove the reference from the conversation
                //var conversation = await _dbContext.ChatConversations.FindAsync(conversationId);
                
                var reference = conversationState.ReferenceItemIds.FirstOrDefault(x => x.Equals(referenceId));
                if (reference == Guid.Empty)
                    return NotFound("Reference not found in conversation");
                await conversationGrain.RemoveConversationReference(reference);
                
                return Ok("Reference removed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reference");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error removing reference");
            }
        }
    }
}
