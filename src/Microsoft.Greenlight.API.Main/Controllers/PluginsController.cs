// File: Microsoft.Greenlight.API.Main/Controllers/PluginController.cs

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing plugins.
    /// This class is now deprecated - please use McpPluginsController instead.
    /// </summary>
    [ApiController]
    [Route("api/plugins")]
    [Obsolete("This controller is deprecated. Please use McpPluginsController instead.")]
    public class PluginsController : BaseController
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginsController"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="mapper">The mapper.</param>
        public PluginsController(
            DocGenerationDbContext dbContext,
            IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Returns a not found response with a message to use the new MCP plugins controller.
        /// </summary>
        /// <returns>A not found response.</returns>
        [HttpGet]
        [HttpPost]
        [HttpPut]
        [HttpDelete]
        [Route("{*path}")]
        public IActionResult HandleLegacyRequests()
        {
            return NotFound("The Dynamic Plugins system has been deprecated. Please use the MCP Plugins system instead. Endpoint: /api/mcp-plugins");
        }
    }
}
