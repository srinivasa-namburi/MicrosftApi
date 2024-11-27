// File: Microsoft.Greenlight.API.Main/Controllers/PluginController.cs

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.Greenlight.API.Main.Controllers;

public class PluginsController : BaseController
{
    private readonly IPluginService _pluginService;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly AzureFileHelper _fileHelper;

    public PluginsController(
        IPluginService pluginService, 
        DocGenerationDbContext dbContext, 
        IMapper mapper,
        AzureFileHelper fileHelper)
    {
        _pluginService = pluginService;
        _dbContext = dbContext;
        _mapper = mapper;
        _fileHelper = fileHelper;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<DynamicPluginInfo>>> GetAllPlugins()
    {
        var plugins = await _pluginService.GetAllPluginsAsync();
        var pluginInfos = _mapper.Map<List<DynamicPluginInfo>>(plugins);
        return Ok(pluginInfos);
    }

    [HttpGet("{pluginId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
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

    [HttpGet("/api/document-processes/{documentProcessId:guid}/plugins")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<DynamicPluginInfo>>> GetPluginsByDocumentProcessId(Guid documentProcessId)
    {
        var plugins = await _pluginService.GetPluginsByDocumentProcessIdAsync(documentProcessId);
        var pluginInfos = _mapper.Map<List<DynamicPluginInfo>>(plugins);
        return Ok(pluginInfos);
    }
    
    [HttpPost("{pluginId:guid}/{version}/associate/{documentProcessId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssociateWithDocumentProcess(Guid pluginId, Guid documentProcessId, string version)
    {
        try
        {
            await _pluginService.AssociatePluginWithDocumentProcessAsync(pluginId, documentProcessId, version);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{pluginId:guid}/disassociate/{documentProcessId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DisassociateFromDocumentProcess(Guid pluginId, Guid documentProcessId)
    {
        try
        {
            await _pluginService.DisassociatePluginFromDocumentProcessAsync(pluginId, documentProcessId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(DynamicPluginInfo))]
    [SwaggerIgnore]
    public async Task<IActionResult> UploadPlugin([FromForm] IFormFile file)
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

        try
        {
            // Save the plugin zip file to Azure Blob Storage
            var containerName = "plugins";
            var blobFileName = $"{pluginName}/{versionString}/{originalFileName}";

            await using var stream = file.OpenReadStream();
            var blobUrl = await _fileHelper.UploadFileToBlobAsync(
                stream, blobFileName, containerName, overwriteIfExists:true);

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
            var existingVersion = plugin.Versions.FirstOrDefault(v => v.Equals(pluginVersion));
            if (existingVersion != null)
            {
                // Overwrite the existing version
                plugin.Versions.Remove(existingVersion);
            }

            plugin.Versions.Add(pluginVersion);

            await _dbContext.SaveChangesAsync();

            var pluginInfo = _mapper.Map<DynamicPluginInfo>(plugin);

            return Ok(pluginInfo);
        }
        catch (Exception ex)
        {
            // Log the exception as needed
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the plugin.");
        }
    }
}