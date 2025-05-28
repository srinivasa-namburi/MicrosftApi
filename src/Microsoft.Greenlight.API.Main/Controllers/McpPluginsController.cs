using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Grains.Shared.Contracts;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing MCP plugins.
    /// </summary>
    [Route("/api/mcp-plugins")]
    public partial class McpPluginsController : BaseController
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly AzureFileHelper _fileHelper;
        private readonly McpPluginManager _pluginManager;
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<McpPluginsController>? _logger;

        // A known GUID for the McpPluginManagementGrain
        private static readonly Guid McpPluginManagementGrainId = new("A7A5C7A5-5C1A-4F8A-8A2E-3A9A8A5C7A9F");

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPluginsController"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="fileHelper">The file helper.</param>
        /// <param name="pluginManager">The plugin manager.</param>
        /// <param name="grainFactory">The Orleans grain factory.</param>
        /// <param name="logger">The logger.</param>
        public McpPluginsController(
            DocGenerationDbContext dbContext,
            IMapper mapper,
            AzureFileHelper fileHelper,
            McpPluginManager pluginManager,
            IGrainFactory grainFactory,
            ILogger<McpPluginsController>? logger = null
        )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _fileHelper = fileHelper;
            _pluginManager = pluginManager;
            _grainFactory = grainFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets all MCP plugins.
        /// </summary>
        /// <returns>A list of all MCP plugins.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<McpPluginInfo>>]
        public async Task<ActionResult<List<McpPluginInfo>>> GetAllMcpPlugins()
        {
            var plugins = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .AsNoTracking()
                .ToListAsync();

            var pluginInfos = _mapper.Map<List<McpPluginInfo>>(plugins);
            return Ok(pluginInfos); // Always returns OK with the list (which might be empty)
        }

        /// <summary>
        /// Gets an MCP plugin by its identifier.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>The plugin information.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     404 Not Found: When the plugin was not found
        /// </returns>
        [HttpGet("{pluginId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<McpPluginInfo>]
        public async Task<ActionResult<McpPluginInfo>> GetMcpPluginById(Guid pluginId)
        {
            var plugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == pluginId);

            if (plugin == null)
            {
                return NotFound();
            }

            var pluginInfo = _mapper.Map<McpPluginInfo>(plugin);
            return Ok(pluginInfo);
        }

        /// <summary>
        /// Gets MCP plugins by document process identifier.
        /// </summary>
        /// <param name="documentProcessId">The document process identifier.</param>
        /// <returns>A list of plugin associations for the document process.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        /// </returns>
        [HttpGet("/api/document-processes/{documentProcessId:guid}/mcp-plugins")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<McpPluginAssociationInfo>>]
        public async Task<ActionResult<List<McpPluginAssociationInfo>>> GetMcpPluginsByDocumentProcessId(Guid documentProcessId)
        {
            // Optimize query to only get necessary data for associations
            var associations = await _dbContext.McpPluginDocumentProcesses
                .Where(dp => dp.DynamicDocumentProcessDefinitionId == documentProcessId)
                .Select(dp => new 
                {
                    dp.Id,
                    dp.McpPluginId,
                    dp.DynamicDocumentProcessDefinitionId,
                    dp.KeepOnLatestVersion,
                    dp.Version,
                    dp.McpPlugin.Name,
                    dp.McpPlugin.Versions
                })
                .AsNoTracking()
                .ToListAsync();

            var result = associations.Select(a => new McpPluginAssociationInfo
            {
                AssociationId = a.Id,
                PluginId = a.McpPluginId,
                DocumentProcessId = a.DynamicDocumentProcessDefinitionId,
                Name = a.Name,
                KeepOnLatestVersion = a.KeepOnLatestVersion,
                CurrentVersion = _mapper.Map<McpPluginVersionInfo>(a.Version),
                AvailableVersions = _mapper.Map<List<McpPluginVersionInfo>>(a.Versions.OrderByDescending(v => v.Major)
                    .ThenByDescending(v => v.Minor)
                    .ThenByDescending(v => v.Patch))
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Gets document processes associated with a specific MCP plugin, with optimized data transfer
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>A list of associations for the plugin
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        /// </returns>
        [HttpGet("{pluginId:guid}/document-process-associations")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<McpPluginAssociationInfo>>]
        public async Task<ActionResult<List<McpPluginAssociationInfo>>> GetPluginAssociations(Guid pluginId)
        {
            var associations = await _dbContext.McpPluginDocumentProcesses
                .Where(dp => dp.McpPluginId == pluginId)
                .Select(dp => new
                {
                    dp.Id,
                    dp.McpPluginId,
                    dp.DynamicDocumentProcessDefinitionId,
                    dp.KeepOnLatestVersion,
                    dp.Version,
                    ProcessName = dp.DynamicDocumentProcessDefinition.ShortName,
                    dp.McpPlugin.Versions
                })
                .AsNoTracking()
                .ToListAsync();

            var result = associations.Select(a => new McpPluginAssociationInfo
            {
                AssociationId = a.Id,
                PluginId = a.McpPluginId,
                DocumentProcessId = a.DynamicDocumentProcessDefinitionId,
                Name = a.ProcessName ?? "Unknown Process",
                KeepOnLatestVersion = a.KeepOnLatestVersion,
                CurrentVersion = _mapper.Map<McpPluginVersionInfo>(a.Version),
                AvailableVersions = _mapper.Map<List<McpPluginVersionInfo>>(a.Versions.OrderByDescending(v => v.Major)
                    .ThenByDescending(v => v.Minor)
                    .ThenByDescending(v => v.Patch))
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Gets document processes associated with a specific MCP plugin.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>A list of document processes associated with the plugin.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        /// </returns>
        [HttpGet("{pluginId:guid}/document-processes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        public async Task<ActionResult<List<DocumentProcessInfo>>> GetDocumentProcessesByPluginId(Guid pluginId)
        {

            var documentProcesses = await _dbContext.McpPluginDocumentProcesses
                .Where(mpd => mpd.McpPluginId == pluginId)
                .Include(mpd => mpd.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .Select(mpd => mpd.DynamicDocumentProcessDefinition)
                .ToListAsync();

            if (documentProcesses == null || !documentProcesses.Any())
            {
                // Return an empty list if no document processes are found
                return Ok(new List<DocumentProcessInfo>());
            }

            var documentProcessInfos = _mapper.Map<List<DocumentProcessInfo>>(documentProcesses);
            return Ok(documentProcessInfos);
        }

        /// <summary>
        /// Associates an MCP plugin with a document process (with support for KeepOnLatestVersion).
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="documentProcessId">The document process identifier.</param>
        /// <param name="version">The plugin version.</param>
        /// <param name="keepOnLatestVersion">Whether to always use the latest version.</param>
        /// <returns>An action result.
        /// Produces Status Codes:
        ///     204 No Content: When completed successfully
        ///     400 Bad Request: When parameters are invalid
        ///     404 Not Found: When the plugin or document process was not found
        /// </returns>
        [HttpPost("{pluginId:guid}/{version}/associate/{documentProcessId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssociateMcpPluginWithDocumentProcess(
            Guid pluginId, Guid documentProcessId, string version, [FromQuery] bool keepOnLatestVersion = false)
        {
            try
            {
                // Verify the plugin exists
                var plugin = await _dbContext.McpPlugins
                    .Include(p => p.Versions)
                    .FirstOrDefaultAsync(p => p.Id == pluginId);

                if (plugin == null)
                {
                    return NotFound($"MCP plugin with ID {pluginId} not found.");
                }

                // Verify the document process exists
                var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                    .FirstOrDefaultAsync(d => d.Id == documentProcessId);

                if (documentProcess == null)
                {
                    return NotFound($"Document process with ID {documentProcessId} not found.");
                }

                // Parse and verify the version
                if (!McpPluginVersion.TryParse(version, out var parsedVersion))
                {
                    return BadRequest($"Invalid version format: {version}. Expected format: Major.Minor.Patch");
                }

                // Find the specified version
                var pluginVersion = plugin.Versions.FirstOrDefault(v =>
                    v.Major == parsedVersion.Major &&
                    v.Minor == parsedVersion.Minor &&
                    v.Patch == parsedVersion.Patch);

                if (pluginVersion == null && !keepOnLatestVersion)
                {
                    return NotFound($"Version {version} not found for plugin {plugin.Name}.");
                }

                // Check if association already exists
                var existingAssociation = await _dbContext.McpPluginDocumentProcesses
                    .FirstOrDefaultAsync(p =>
                        p.McpPluginId == pluginId &&
                        p.DynamicDocumentProcessDefinitionId == documentProcessId);

                if (existingAssociation != null)
                {
                    // Update the existing association
                    existingAssociation.VersionId = keepOnLatestVersion ? null : pluginVersion?.Id;
                    existingAssociation.KeepOnLatestVersion = keepOnLatestVersion;
                    existingAssociation.IsEnabled = true;
                }
                else
                {
                    // Create a new association
                    var association = new McpPluginDocumentProcess
                    {
                        McpPluginId = pluginId,
                        DynamicDocumentProcessDefinitionId = documentProcessId,
                        VersionId = keepOnLatestVersion ? null : pluginVersion?.Id,
                        KeepOnLatestVersion = keepOnLatestVersion,
                        IsEnabled = true
                    };

                    _dbContext.McpPluginDocumentProcesses.Add(association);
                }

                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Updates the version or KeepOnLatestVersion flag of an MCP plugin for a document process.
        /// </summary>
        /// <param name="documentProcessId">The document process identifier.</param>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="update">The update info (version and/or KeepOnLatestVersion).</param>
        /// <returns>An action result.
        /// Produces Status Codes:
        ///     204 No Content: When completed successfully
        ///     400 Bad Request: When parameters are invalid
        ///     404 Not Found: When the plugin or document process was not found
        /// </returns>
        [HttpPut("/api/document-processes/{documentProcessId:guid}/mcp-plugins/{pluginId:guid}/association")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMcpPluginAssociation(
            Guid documentProcessId, Guid pluginId, [FromBody] McpPluginAssociationInfo update)
        {
            try
            {
                // Verify the plugin exists
                var plugin = await _dbContext.McpPlugins
                    .Include(p => p.Versions)
                    .FirstOrDefaultAsync(p => p.Id == pluginId);

                if (plugin == null)
                {
                    return NotFound($"MCP plugin with ID {pluginId} not found.");
                }

                // Verify the document process exists
                var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                    .FirstOrDefaultAsync(d => d.Id == documentProcessId);

                if (documentProcess == null)
                {
                    return NotFound($"Document process with ID {documentProcessId} not found.");
                }

                // Check if association exists
                var existingAssociation = await _dbContext.McpPluginDocumentProcesses
                    .FirstOrDefaultAsync(p =>
                        p.McpPluginId == pluginId &&
                        p.DynamicDocumentProcessDefinitionId == documentProcessId);

                if (existingAssociation == null)
                {
                    return NotFound($"No association found between plugin {plugin.Name} and document process ID {documentProcessId}.");
                }

                if (update.KeepOnLatestVersion)
                {
                    existingAssociation.VersionId = null;
                    existingAssociation.KeepOnLatestVersion = true;
                }
                else if (update.CurrentVersion != null)
                {
                    // Find the specified version
                    var pluginVersion = plugin.Versions.FirstOrDefault(v =>
                        v.Major == update.CurrentVersion.Major &&
                        v.Minor == update.CurrentVersion.Minor &&
                        v.Patch == update.CurrentVersion.Patch);

                    if (pluginVersion == null)
                    {
                        return NotFound($"Version {update.CurrentVersion.Major}.{update.CurrentVersion.Minor}.{update.CurrentVersion.Patch} not found for plugin {plugin.Name}.");
                    }

                    existingAssociation.VersionId = pluginVersion.Id;
                    existingAssociation.KeepOnLatestVersion = false;
                }

                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Disassociates an MCP plugin from a document process.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="documentProcessId">The document process identifier.</param>
        /// <returns>An action result.
        /// Produces Status Codes:
        ///     204 No Content: When completed successfully
        /// </returns>
        [HttpPost("{pluginId:guid}/disassociate/{documentProcessId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DisassociateMcpPluginFromDocumentProcess(Guid pluginId, Guid documentProcessId)
        {
            var association = await _dbContext.McpPluginDocumentProcesses
                .FirstOrDefaultAsync(p =>
                    p.McpPluginId == pluginId &&
                    p.DynamicDocumentProcessDefinitionId == documentProcessId);

            if (association != null)
            {
                _dbContext.McpPluginDocumentProcesses.Remove(association);
                await _dbContext.SaveChangesAsync();
            }

            return NoContent();
        }

        /// <summary>
        /// Uploads an MCP plugin and checks for manifest.json.
        /// </summary>
        /// <param name="file">The plugin file.</param>
        /// <returns>The uploaded plugin information and a flag indicating if overrides are needed.</returns>
        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [Produces<McpPluginUploadResponse>]
        public async Task<ActionResult<McpPluginUploadResponse>> UploadMcpPlugin([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No plugin file uploaded.");
            }

            // Ensure the file is a zip archive
            var originalFileName = file.FileName;

            if (!Path.GetExtension(originalFileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid file type. Only zip files are allowed.");
            }

            // Extract plugin name and version from the file name
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            var underscoreIndex = fileNameWithoutExtension.LastIndexOf('_');

            if (underscoreIndex == -1)
            {
                return BadRequest("Invalid plugin file name format. Expected format: PluginName_version.zip");
            }

            var pluginName = fileNameWithoutExtension.Substring(0, underscoreIndex);
            var versionString = fileNameWithoutExtension.Substring(underscoreIndex + 1);

            if (!McpPluginVersion.TryParse(versionString, out var pluginVersion))
            {
                return BadRequest("Invalid plugin version format. Expected format: Major.Minor.Patch");
            }

            // Save the plugin zip file to Azure Blob Storage
            var containerName = "mcp-plugins";
            var blobFileName = $"{pluginName}/{versionString}/{originalFileName}";

            await using var stream = file.OpenReadStream();
            var blobUrl = await _fileHelper.UploadFileToBlobAsync(
                stream, blobFileName, containerName, overwriteIfExists: true);

            // Save or update plugin information in the database
            var plugin = await _dbContext.McpPlugins
                .FirstOrDefaultAsync(p => p.Name == pluginName);

            if (plugin == null)
            {
                // Create a new plugin entry
                plugin = new McpPlugin
                {
                    Name = pluginName,
                    BlobContainerName = containerName,
                    SourceType = McpPluginSourceType.AzureBlobStorage,
                    Versions = new List<McpPluginVersion>(),
                    DocumentProcesses = new List<McpPluginDocumentProcess>()
                };

                _dbContext.McpPlugins.Add(plugin);
            }

            // Check if this version already exists
            var existingVersion = await _dbContext.McpPluginVersions
                .FirstOrDefaultAsync(v =>
                    v.McpPluginId == plugin.Id &&
                    v.Major == pluginVersion.Major &&
                    v.Minor == pluginVersion.Minor &&
                    v.Patch == pluginVersion.Patch);

            if (existingVersion == null)
            {
                // Add the new version
                existingVersion = new McpPluginVersion
                {
                    Major = pluginVersion.Major,
                    Minor = pluginVersion.Minor,
                    Patch = pluginVersion.Patch,
                    McpPluginId = plugin.Id,
                    Arguments = new List<string>(),
                    EnvironmentVariables = new Dictionary<string, string>()
                };

                _dbContext.McpPluginVersions.Add(existingVersion);
            }

            await _dbContext.SaveChangesAsync();

            // Process the uploaded plugin to check for manifest.json
            var pluginStream = await _fileHelper.GetFileAsStreamFromContainerAndBlobName(containerName, blobFileName);
            var needsOverride = await _pluginManager.ProcessUploadedPluginAsync(pluginStream!, plugin, existingVersion);

            // Ensure manifest values are persisted
            await _dbContext.SaveChangesAsync();

            // Reload the plugin with its versions to get the complete data
            var updatedPlugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .FirstAsync(p => p.Id == plugin.Id);

            // Notify the grain about the update
            // Enumerate all versions and stop them
            foreach (var version in updatedPlugin.Versions)
            {
                try
                {
                    var managementGrain = _grainFactory.GetGrain<IMcpPluginManagementGrain>(McpPluginManagementGrainId);
                    await managementGrain.StopAndRemovePluginVersionAsync(plugin.Name, version.ToString());
                    _logger?.LogInformation("Successfully notified McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error notifying McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
            }

            var pluginInfo = _mapper.Map<McpPluginInfo>(updatedPlugin);
            return Ok(new McpPluginUploadResponse
            {
                PluginInfo = pluginInfo,
                NeedsOverride = needsOverride
            });
        }

        /// <summary>
        /// Creates a command-line only MCP plugin without a package file.
        /// </summary>
        /// <param name="createModel">Information for creating the plugin.</param>
        /// <returns>The created plugin information.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     400 Bad Request: When parameters are invalid
        /// </returns>
        [HttpPost("command-only")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/json")]
        [Produces<McpPluginInfo>]
        public async Task<ActionResult<McpPluginInfo>> CreateCommandOnlyMcpPlugin([FromBody] CommandOnlyMcpPluginCreateModel createModel)
        {
            if (createModel == null)
            {
                return BadRequest("No plugin information provided.");
            }

            if (string.IsNullOrWhiteSpace(createModel.Name))
            {
                return BadRequest("Plugin name is required.");
            }

            if (string.IsNullOrWhiteSpace(createModel.Command))
            {
                return BadRequest("Command is required.");
            }

            if (!McpPluginVersion.TryParse(createModel.Version, out var pluginVersion))
            {
                return BadRequest("Invalid version format. Expected format: Major.Minor.Patch");
            }

            // Save or update plugin information in the database
            var plugin = await _dbContext.McpPlugins.Include(mcpPlugin => mcpPlugin.Versions)
                .FirstOrDefaultAsync(p => p.Name == createModel.Name);

            if (plugin == null)
            {
                // Create a new plugin entry
                plugin = new McpPlugin
                {
                    Name = createModel.Name,
                    Description = createModel.Description,
                    SourceType = McpPluginSourceType.CommandOnly,
                    Versions = new List<McpPluginVersion>(),
                    DocumentProcesses = new List<McpPluginDocumentProcess>()
                };

                _dbContext.McpPlugins.Add(plugin);
            }
            else if (plugin.SourceType != McpPluginSourceType.CommandOnly)
            {
                return BadRequest($"Plugin '{createModel.Name}' already exists with a different source type: {plugin.SourceType}");
            }

            // Check if this version already exists
            var existingVersion = await _dbContext.McpPluginVersions
                .FirstOrDefaultAsync(v =>
                    v.McpPluginId == plugin.Id &&
                    v.Major == pluginVersion.Major &&
                    v.Minor == pluginVersion.Minor &&
                    v.Patch == pluginVersion.Patch);

            if (existingVersion != null)
            {
                // Update the existing version
                existingVersion.Command = createModel.Command;
                existingVersion.Arguments = createModel.Arguments ?? new List<string>();
                existingVersion.EnvironmentVariables = createModel.EnvironmentVariables ?? new Dictionary<string, string>();

                _dbContext.McpPluginVersions.Update(existingVersion);
            }
            else
            {
                // Add the new version
                var newVersion = new McpPluginVersion
                {
                    Major = pluginVersion.Major,
                    Minor = pluginVersion.Minor,
                    Patch = pluginVersion.Patch,
                    Command = createModel.Command,
                    Arguments = createModel.Arguments ?? new List<string>(),
                    EnvironmentVariables = createModel.EnvironmentVariables ?? new Dictionary<string, string>(),
                    McpPluginId = plugin.Id
                };

                _dbContext.McpPluginVersions.Add(newVersion);
            }

            await _dbContext.SaveChangesAsync();
            
            // Reload the plugin with its versions to get the complete data
            var updatedPlugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .FirstAsync(p => p.Id == plugin.Id);

            // Notify the grain about the update
            // Enumerate all versions and stop them
            foreach (var version in updatedPlugin.Versions)
            {
                try
                {
                    var managementGrain = _grainFactory.GetGrain<IMcpPluginManagementGrain>(McpPluginManagementGrainId);
                    await managementGrain.StopAndRemovePluginVersionAsync(plugin.Name, version.ToString());
                    _logger?.LogInformation("Successfully notified McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error notifying McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
            }

            var pluginInfo = _mapper.Map<McpPluginInfo>(updatedPlugin);
            return Ok(pluginInfo);
        }

        /// <summary>
        /// Creates an SSE/HTTP plugin that connects to a remote endpoint.
        /// </summary>
        /// <param name="createModel">Information for creating the plugin.</param>
        /// <returns>The created plugin information.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     400 Bad Request: When parameters are invalid
        /// </returns>
        [HttpPost("sse")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/json")]
        [Produces<McpPluginInfo>]
        public async Task<ActionResult<McpPluginInfo>> CreateSseMcpPlugin([FromBody] SseMcpPluginCreateModel createModel)
        {
            if (createModel == null)
            {
                return BadRequest("No plugin information provided.");
            }

            if (string.IsNullOrWhiteSpace(createModel.Name))
            {
                return BadRequest("Plugin name is required.");
            }

            if (string.IsNullOrWhiteSpace(createModel.Url))
            {
                return BadRequest("Endpoint URL is required.");
            }

            if (!McpPluginVersion.TryParse(createModel.Version, out var pluginVersion))
            {
                return BadRequest("Invalid version format. Expected format: Major.Minor.Patch");
            }

            // Save or update plugin information in the database
            var plugin = await _dbContext.McpPlugins.Include(mcpPlugin => mcpPlugin.Versions)
                .FirstOrDefaultAsync(p => p.Name == createModel.Name);

            if (plugin == null)
            {
                // Create a new plugin entry
                plugin = new McpPlugin
                {
                    Name = createModel.Name,
                    Description = createModel.Description,
                    SourceType = McpPluginSourceType.SSE,
                    Versions = new List<McpPluginVersion>(),
                    DocumentProcesses = new List<McpPluginDocumentProcess>()
                };

                _dbContext.McpPlugins.Add(plugin);
            }
            else if (plugin.SourceType != McpPluginSourceType.SSE)
            {
                return BadRequest($"Plugin '{createModel.Name}' already exists with a different source type: {plugin.SourceType}");
            }

            // Check if this version already exists
            var existingVersion = await _dbContext.McpPluginVersions
                .FirstOrDefaultAsync(v =>
                    v.McpPluginId == plugin.Id &&
                    v.Major == pluginVersion.Major &&
                    v.Minor == pluginVersion.Minor &&
                    v.Patch == pluginVersion.Patch);

            if (existingVersion != null)
            {
                // Update the existing version
                existingVersion.Url = createModel.Url;
                existingVersion.AuthenticationType = createModel.AuthenticationType;

                _dbContext.McpPluginVersions.Update(existingVersion);
            }
            else
            {
                // Add the new version
                var newVersion = new McpPluginVersion
                {
                    Major = pluginVersion.Major,
                    Minor = pluginVersion.Minor,
                    Patch = pluginVersion.Patch,
                    Url = createModel.Url,
                    AuthenticationType = createModel.AuthenticationType,
                    McpPluginId = plugin.Id,
                    // Initialize empty collections for consistency
                    Arguments = new List<string>(),
                    EnvironmentVariables = new Dictionary<string, string>()
                };

                _dbContext.McpPluginVersions.Add(newVersion);
            }

            await _dbContext.SaveChangesAsync();

            // Try to load the plugin to verify it can be instantiated
            try
            {
                await _pluginManager.LoadPluginByIdAsync(plugin.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to verify SSE plugin functionality. Plugin was created but may not work correctly.");
            }

            // Reload the plugin with its versions to get the complete data
            var updatedPlugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .FirstAsync(p => p.Id == plugin.Id);

            // Notify the grain about the update
            // Enumerate all versions and stop them
            foreach (var version in updatedPlugin.Versions)
            {
                try
                {
                    var managementGrain = _grainFactory.GetGrain<IMcpPluginManagementGrain>(McpPluginManagementGrainId);
                    await managementGrain.StopAndRemovePluginVersionAsync(plugin.Name, version.ToString());
                    _logger?.LogInformation("Successfully notified McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error notifying McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
            }

            var pluginInfo = _mapper.Map<McpPluginInfo>(updatedPlugin);
            return Ok(pluginInfo);
        }

        /// <summary>
        /// Updates an MCP plugin of any source type.
        /// </summary>
        /// <param name="updateModel">Information for updating the plugin.</param>
        /// <returns>The updated plugin information.
        /// Produces Status Codes:
        ///     200 OK: When completed successfully
        ///     400 Bad Request: When parameters are invalid
        ///     404 Not Found: When the plugin was not found
        /// </returns>
        [HttpPut("update")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<McpPluginInfo>]
        public async Task<ActionResult<McpPluginInfo>> UpdateMcpPlugin([FromBody] McpPluginUpdateModel updateModel)
        {
            if (updateModel == null)
            {
                return BadRequest("No plugin information provided.");
            }

            // Find the plugin
            var plugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Id == updateModel.Id);

            if (plugin == null)
            {
                return NotFound($"MCP plugin with ID {updateModel.Id} not found.");
            }

            // Update plugin metadata
            plugin.Name = updateModel.Name;
            plugin.Description = updateModel.Description;

            // Parse version
            if (!McpPluginVersion.TryParse(updateModel.Version, out var pluginVersion))
            {
                return BadRequest("Invalid version format. Expected format: Major.Minor.Patch");
            }

            // Find or create the version
            var existingVersion = plugin.Versions.FirstOrDefault(v =>
                v.Major == pluginVersion.Major &&
                v.Minor == pluginVersion.Minor &&
                v.Patch == pluginVersion.Patch);

            if (existingVersion != null)
            {
                // Update existing version based on plugin type
                if (plugin.SourceType == McpPluginSourceType.SSE)
                {
                    // Update SSE-specific properties
                    existingVersion.Url = updateModel.Url;
                    existingVersion.AuthenticationType = updateModel.AuthenticationType;
                }
                else
                {
                    // Update Command-based properties
                    if (!string.IsNullOrEmpty(updateModel.Command))
                    {
                        existingVersion.Command = updateModel.Command;
                    }

                    if (updateModel.Arguments != null)
                    {
                        existingVersion.Arguments = updateModel.Arguments;
                    }

                    if (updateModel.EnvironmentVariables != null)
                    {
                        existingVersion.EnvironmentVariables = updateModel.EnvironmentVariables;
                    }
                }

                _dbContext.McpPluginVersions.Update(existingVersion);
            }
            else
            {
                // Add new version based on plugin type
                var newVersion = new McpPluginVersion
                {
                    Major = pluginVersion.Major,
                    Minor = pluginVersion.Minor,
                    Patch = pluginVersion.Patch,
                    McpPluginId = plugin.Id
                };

                if (plugin.SourceType == McpPluginSourceType.SSE)
                {
                    // Set SSE-specific properties
                    newVersion.Url = updateModel.Url;
                    newVersion.AuthenticationType = updateModel.AuthenticationType;
                }
                else
                {
                    // Set Command-based properties
                    newVersion.Command = updateModel.Command;
                    newVersion.Arguments = updateModel.Arguments ?? new List<string>();
                    newVersion.EnvironmentVariables = updateModel.EnvironmentVariables ?? new Dictionary<string, string>();
                }

                _dbContext.McpPluginVersions.Add(newVersion);
            }

            _dbContext.McpPlugins.Update(plugin);
            await _dbContext.SaveChangesAsync();
            
            // Reload the plugin with its versions to get the complete data
            var updatedPlugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .FirstAsync(p => p.Id == plugin.Id);

            // Notify the grain about the update
            // Enumerate all versions and stop them
            foreach (var version in updatedPlugin.Versions)
            {
                try
                {
                    var managementGrain = _grainFactory.GetGrain<IMcpPluginManagementGrain>(McpPluginManagementGrainId);
                    await managementGrain.StopAndRemovePluginVersionAsync(plugin.Name, version.ToString());
                    _logger?.LogInformation("Successfully notified McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error notifying McpPluginManagementGrain to stop plugin {PluginName} version {Version}", plugin.Name, version.ToString());
                }
            }

            var pluginInfo = _mapper.Map<McpPluginInfo>(updatedPlugin);
            return Ok(pluginInfo);
        }

        /// <summary>
        /// Deletes an MCP plugin.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>No content if successful.
        /// Produces Status Codes:
        ///     204 No Content: When completed successfully
        ///     404 Not Found: When the plugin was not found
        /// </returns>
        [HttpDelete("{pluginId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMcpPlugin(Guid pluginId)
        {
            var plugin = await _dbContext.McpPlugins
                .Include(p => p.Versions)
                .Include(p => p.DocumentProcesses)
                .FirstOrDefaultAsync(p => p.Id == pluginId);

            if (plugin == null)
            {
                return NotFound();
            }

            try
            {
                // First remove all document process associations
                if (plugin.DocumentProcesses != null && plugin.DocumentProcesses.Any())
                {
                    _dbContext.McpPluginDocumentProcesses.RemoveRange(plugin.DocumentProcesses);
                }

                // Remove all versions
                if (plugin.Versions.Any())
                {
                    // Notify the grain about the deletion
                    try
                    {
                        var managementGrain = _grainFactory.GetGrain<IMcpPluginManagementGrain>(McpPluginManagementGrainId);
                        if (plugin.Versions != null)
                        {
                            foreach (var version in plugin.Versions)
                            {
                                await managementGrain.StopAndRemovePluginVersionAsync(plugin.Name, version.ToString());
                                _logger?.LogInformation(
                                    "Successfully notified McpPluginManagementGrain to stop plugin {PluginName} version {Version}",
                                    plugin.Name, version.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error notifying McpPluginManagementGrain to stop plugin {PluginName}", plugin.Name);
                    }

                    _dbContext.McpPluginVersions.RemoveRange(plugin.Versions);
                }

                // If it's a blob storage plugin, we should try to delete the files
                // This might need to be improved to handle files more robustly
                if (plugin.SourceType == McpPluginSourceType.AzureBlobStorage &&
                    !string.IsNullOrEmpty(plugin.BlobContainerName))
                {
                    try
                    {
                        // Delete the blob associated with the plugin in storage
                        await _fileHelper.DeleteBlobAsync(plugin.BlobContainerName, plugin.Name);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - we still want to delete the database entry
                        _logger?.LogWarning(ex, "Failed to delete blob files for plugin: {PluginName}", plugin.Name);
                    }
                }

                // Delete the plugin itself
                _dbContext.McpPlugins.Remove(plugin);
                await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting MCP plugin: {PluginId}", pluginId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting plugin: {ex.Message}");
            }
        }
    }
}
