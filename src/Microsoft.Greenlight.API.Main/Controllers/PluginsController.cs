// File: Microsoft.Greenlight.API.Main/Controllers/PluginController.cs

using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing plugins.
/// </summary>
public class PluginsController : BaseController
{
    private readonly IPluginService _pluginService;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly AzureFileHelper _fileHelper;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginsController"/> class.
    /// </summary>
    /// <param name="pluginService">The plugin service.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The mapper.</param>
    /// <param name="fileHelper">The file helper.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    public PluginsController(
        IPluginService pluginService,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        AzureFileHelper fileHelper,
        IPublishEndpoint publishEndpoint
    )
    {
        _pluginService = pluginService;
        _dbContext = dbContext;
        _mapper = mapper;
        _fileHelper = fileHelper;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Gets all plugins.
    /// </summary>
    /// <returns>A list of all plugins.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<DynamicPluginInfo>>]
    public async Task<ActionResult<List<DynamicPluginInfo>>> GetAllPlugins()
    {
        var plugins = await _pluginService.GetAllPluginsAsync();
        var pluginInfos = _mapper.Map<List<DynamicPluginInfo>>(plugins);
        return Ok(pluginInfos);
    }

    /// <summary>
    /// Gets a plugin by its identifier.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the plugin information was not found using the plugin id provided
    /// </returns>
    [HttpGet("{pluginId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<DynamicPluginInfo>]
    public async Task<ActionResult<DynamicPluginInfo>> GetPluginById(Guid pluginId)
    {
        var plugin = await _pluginService.GetPluginByIdAsync(pluginId);
        if (plugin == null)
        {
            return NotFound();
        }
        var pluginInfo = _mapper.Map<DynamicPluginInfo>(plugin);
        return Ok(pluginInfo);
    }

    /// <summary>
    /// Gets plugins by document process identifier.
    /// </summary>
    /// <param name="documentProcessId">The document process identifier.</param>
    /// <returns>A list of plugins associated with the document process.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpGet("/api/document-processes/{documentProcessId:guid}/plugins")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<DynamicPluginInfo>>]
    public async Task<ActionResult<List<DynamicPluginInfo>>> GetPluginsByDocumentProcessId(Guid documentProcessId)
    {
        var plugins = await _pluginService.GetPluginsByDocumentProcessIdAsync(documentProcessId);
        var pluginInfos = _mapper.Map<List<DynamicPluginInfo>>(plugins);
        return Ok(pluginInfos);
    }

    /// <summary>
    /// Associates a plugin with a document process.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="documentProcessId">The document process identifier.</param>
    /// <param name="version">The plugin version.</param>
    /// <returns>An action result.
    /// Produces Status Codes:
    ///     204 No Content: When completed sucessfully
    ///     400 Bad Request: When either the plugin could not be found or the document process could not be found, 
    ///     the version provided does not conform to the major.minor.patch format, 
    ///     or the plugin version provided could not be found
    /// </returns>
    [HttpPost("{pluginId:guid}/{version}/associate/{documentProcessId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateWithDocumentProcess(Guid pluginId, Guid documentProcessId, string version)
    {
        try
        {
            await _pluginService.AssociatePluginWithDocumentProcessAsync(pluginId, documentProcessId, version);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Disassociates a plugin from a document process.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="documentProcessId">The document process identifier.</param>
    /// <returns>An action result.
    /// Produces Status Codes:
    ///     204 No Content: When completed sucessfully
    /// </returns>
    [HttpPost("{pluginId:guid}/disassociate/{documentProcessId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisassociateFromDocumentProcess(Guid pluginId, Guid documentProcessId)
    {
        await _pluginService.DisassociatePluginFromDocumentProcessAsync(pluginId, documentProcessId);
        return NoContent();
    }

    /// <summary>
    /// Uploads a plugin.
    /// </summary>
    /// <param name="file">The plugin file.</param>
    /// <returns>The uploaded plugin information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When no file is provided, an invalid file type is provided, 
    ///     the format of the file name doesn't conform to PluginName_version.zip, 
    ///     the version provided does not conform to the major.minor.patch format, 
    /// </returns>
    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [Produces<DynamicPluginInfo>]
    [SwaggerIgnore]
    public async Task<ActionResult<DynamicPluginInfo>> UploadPlugin([FromForm] IFormFile file)
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
        // Expected format: PluginName_version.zip (e.g., Microsoft.Greenlight.Demos.PluginDemo_1.0.0.zip)
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var underscoreIndex = fileNameWithoutExtension.LastIndexOf('_');

        if (underscoreIndex == -1)
        {
            return BadRequest("Invalid plugin file name format. Expected format: PluginName_version.zip");
        }

        var pluginName = fileNameWithoutExtension.Substring(0, underscoreIndex);
        var versionString = fileNameWithoutExtension.Substring(underscoreIndex + 1);

        if (!DynamicPluginVersion.TryParse(versionString, out var pluginVersion))
        {
            return BadRequest("Invalid plugin version format. Expected format: Major.Minor.Patch");
        }

        // Save the plugin zip file to Azure Blob Storage
        var containerName = "plugins";
        var blobFileName = $"{pluginName}/{versionString}/{originalFileName}";

        await using var stream = file.OpenReadStream();
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(
            stream, blobFileName, containerName, overwriteIfExists: true);

        // Save or update plugin information in the database
        var plugin = await _dbContext.DynamicPlugins
            .FirstOrDefaultAsync(p => p.Name == pluginName);

        if (plugin == null)
        {
            // Create a new plugin entry
            plugin = new DynamicPlugin
            {
                Name = pluginName,
                BlobContainerName = containerName,
                Versions = new List<DynamicPluginVersion>(),
                DocumentProcesses = new List<DynamicPluginDocumentProcess>()
            };

            _dbContext.DynamicPlugins.Add(plugin);
        }

        // Check if this version already exists
        plugin.Versions ??= new List<DynamicPluginVersion>();
        var existingVersion = plugin.Versions.FirstOrDefault(v => v.Equals(pluginVersion));
        if (existingVersion is not null)
        {
            // Overwrite the existing version
            plugin.Versions.Remove(existingVersion);
        }

        plugin.Versions.Add(pluginVersion!);

        await _dbContext.SaveChangesAsync();

        var pluginInfo = _mapper.Map<DynamicPluginInfo>(plugin);

        if (AdminHelper.IsRunningInProduction())
        {
            await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
        }

        return Ok(pluginInfo);
    }
}
