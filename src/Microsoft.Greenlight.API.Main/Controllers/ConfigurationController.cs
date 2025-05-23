using AutoMapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using System.Text.Json;
using System.Text.RegularExpressions;
using Orleans.Streams;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling configuration-related requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : BaseController
{
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _serviceConfigurationOptionsMonitor;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ILogger<ConfigurationController> _logger;
    private readonly EfCoreConfigurationProvider _configProvider;
    private readonly IConfiguration _configuration;
    private readonly IClusterClient _clusterClient;

    /// <summary>
    /// A list of accepted keys for configuration updates, including wildcards.
    /// A "*" wildcard matches any single key segment (without a deep match).
    /// A "**" wildcard matches any key segment (with a deep match).
    /// </summary>
    private static readonly List<string> AcceptedKeys =
    [
        "ServiceConfiguration:GreenlightServices:FrontEnd:*",
        "ServiceConfiguration:GreenlightServices:FeatureFlags:*",
        "ServiceConfiguration:GreenlightServices:ReferenceIndexing:*",
        "ServiceConfiguration:GreenlightServices:Scalability:*",
        "ServiceConfiguration:OpenAI:*"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
    /// </summary>
    /// <param name="serviceConfigurationOptionsMonitor">The service configuration options monitor.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The mapper.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configProvider">EF Core based configuration provider</param>
    /// <param name="configuration">Runtime IConfiguration</param>
    /// <param name="clusterClient">Orleans cluster client</param>
    public ConfigurationController(
        IOptionsMonitor<ServiceConfigurationOptions> serviceConfigurationOptionsMonitor,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        ILogger<ConfigurationController> logger,
        EfCoreConfigurationProvider configProvider,
        IConfiguration configuration,
        IClusterClient clusterClient)
    {
        _serviceConfigurationOptionsMonitor = serviceConfigurationOptionsMonitor;
        _dbContext = dbContext;
        _mapper = mapper;
        _logger = logger;
        _configProvider = configProvider;
        _configuration = configuration;
        _clusterClient = clusterClient;
    }

    /// <summary>
    /// Gets the Azure Maps key.
    /// </summary>
    /// <returns>The Azure Maps key.</returns>
    [HttpGet("azure-maps-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<string> GetAzureMapsKey()
    {
        return Ok(_serviceConfigurationOptionsMonitor.CurrentValue.AzureMaps.Key);
    }

    /// <summary>
    /// Gets OpenAI Options
    /// </summary>
    /// <returns></returns>
    [HttpGet("openai-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<ServiceConfigurationOptions.OpenAiOptions> GetOpenAiOptions()
    {
        var configurationOpenAiOptions = _configuration.GetSection("ServiceConfiguration:OpenAI").Get<ServiceConfigurationOptions.OpenAiOptions>();
        return Ok(configurationOpenAiOptions);
    }

    /// <summary>
    /// Gets the document processes.
    /// </summary>
    /// <returns>A list of document process options.</returns>
    [HttpGet("document-processes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<List<DocumentProcessOptions>> GetDocumentProcesses()
    {
        var configurationDocumentProcesses = _configuration.GetSection("ServiceConfiguration:GreenlightServices:DocumentProcesses").Get<List<DocumentProcessOptions>>();
        return Ok(configurationDocumentProcesses);
    }

    /// <summary>
    /// Gets the feature flags.
    /// </summary>
    /// <returns>The feature flags options.</returns>
    [HttpGet("feature-flags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlags()
    {
        var configurationFeatureFlags = _configuration.GetSection("ServiceConfiguration:GreenlightServices:FeatureFlags").Get<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions>();
        return Ok(configurationFeatureFlags);
    }

    /// <summary>
    /// Gets the global options.
    /// </summary>
    [HttpGet("global-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions> GetGlobalOptions()
    {
        var configurationGlobalOptions = _configuration.GetSection("ServiceConfiguration:GreenlightServices:Global").Get<ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions>();
        return Ok(configurationGlobalOptions);
    }

    /// <summary>
    /// Gets the frontend options. This is allowed to be accessed without authentication.
    /// </summary>
    /// <returns>The frontend options.</returns>
    [HttpGet("frontend")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions> GetFrontend()
    {
        var configurationFrontendOptions = _configuration.GetSection("ServiceConfiguration:GreenlightServices:FrontEnd").Get<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions>();
        return Ok(configurationFrontendOptions);
    }

    /// <summary>
    /// Gets the scalability options. 
    /// </summary>
    /// <returns>The scalability options.</returns>
    [HttpGet("scalability-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.ScalabilityOptions> GetScalabilityOptions()
    {
        var configurationScalabilityOptions = _configuration.GetSection("ServiceConfiguration:GreenlightServices:Scalability").Get<ServiceConfigurationOptions.GreenlightServicesOptions.ScalabilityOptions>();
        return Ok(configurationScalabilityOptions);
    }

    /// <summary>
    /// Update the configuration
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Produces("application/json")]
    public async Task<IActionResult> UpdateConfiguration([FromBody] ConfigurationUpdateRequest? request)
    {
        if (request == null || !request.ConfigurationItems.Any())
        {
            return BadRequest("Invalid configuration update request.");
        }

        // Validate keys
        foreach (var key in request.ConfigurationItems.Keys)
        {
            if (!IsAcceptedKey(key))
            {
                return BadRequest($"Invalid configuration key: {key}. The key is not on the accepted list of keys.");
            }
        }

        try
        {
            // Update the database
            var config = await _dbContext.Configurations.FirstOrDefaultAsync(
                c => c.Id == DbConfiguration.DefaultId);

            if (config == null)
            {
                config = new DbConfiguration { ConfigurationValues = "{}", LastUpdated = DateTime.UtcNow, LastUpdatedBy = "System" };
                _dbContext.Configurations.Add(config);
            }

            var configValues = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ConfigurationValues) ?? new Dictionary<string, string>();

            foreach (var item in request.ConfigurationItems)
            {
                configValues[item.Key] = item.Value;
            }

            config.ConfigurationValues = JsonSerializer.Serialize(configValues);
            config.LastUpdated = DateTime.UtcNow;
            config.LastUpdatedBy = "System";

            await _dbContext.SaveChangesAsync();

            // Update the local configuration 
            _configProvider.UpdateOptions(request.ConfigurationItems);

            var configurationUpdatedMessage = new ConfigurationUpdated(Guid.NewGuid());

            // Publish to Orleans Stream
            var streamProvider = _clusterClient.GetStreamProvider("StreamProvider");
            var stream = streamProvider.GetStream<ConfigurationUpdated>(
                SystemStreamNameSpaces.ConfigurationUpdatedNamespace,
                Guid.Empty);

            await stream.OnNextAsync(configurationUpdatedMessage);

            var configInfo = _mapper.Map<DbConfigurationInfo>(config);
            return Ok(configInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration.");
            return StatusCode(500, "Internal server error - Error updating configuration.");
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    /// <returns>The current configuration.</returns>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Produces("application/json")]
    public async Task<IActionResult> GetCurrentConfiguration()
    {
        try
        {
            var config = await _dbContext.Configurations.FirstOrDefaultAsync(
                c => c.Id == DbConfiguration.DefaultId);

            if (config == null)
            {
                return NotFound("Configuration not found.");
            }

            var configInfo = _mapper.Map<DbConfigurationInfo>(config);
            return Ok(configInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration.");
            return StatusCode(500, "Internal server error - Error retrieving configuration.");
        }
    }

    /// <summary>
    /// Gets all AI models.
    /// </summary>
    /// <returns>A list of AI model infos.</returns>
    [HttpGet("ai-models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<AiModelInfo>>> GetAiModels()
    {
        var aiModels = await _dbContext.AiModels.ToListAsync();
        var aiModelInfos = _mapper.Map<List<AiModelInfo>>(aiModels);
        return Ok(aiModelInfos);
    }

    /// <summary>
    /// Gets an AI model by ID.
    /// </summary>
    /// <param name="id">The ID of the AI model to retrieve.</param>
    /// <returns>The AI model info.</returns>
    [HttpGet("ai-models/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelInfo>> GetAiModelById(Guid id)
    {
        var aiModel = await _dbContext.AiModels.FindAsync(id);
        if (aiModel == null)
        {
            return NotFound($"AI model with ID {id} not found.");
        }

        var aiModelInfo = _mapper.Map<AiModelInfo>(aiModel);
        return Ok(aiModelInfo);
    }

    /// <summary>
    /// Creates a new AI model.
    /// </summary>
    /// <param name="aiModelInfo">The AI model to create.</param>
    /// <returns>The created AI model.</returns>
    [HttpPost("ai-models")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelInfo>> CreateAiModel([FromBody] AiModelInfo aiModelInfo)
    {
        if (aiModelInfo == null)
        {
            return BadRequest("AI model info is required.");
        }

        var aiModel = _mapper.Map<AiModel>(aiModelInfo);
        _dbContext.AiModels.Add(aiModel);
        await _dbContext.SaveChangesAsync();

        aiModelInfo = _mapper.Map<AiModelInfo>(aiModel);
        return CreatedAtAction(nameof(GetAiModelById), new { id = aiModel.Id }, aiModelInfo);
    }

    /// <summary>
    /// Updates an existing AI model.
    /// </summary>
    /// <param name="id">The ID of the AI model to update.</param>
    /// <param name="aiModelInfo">The updated AI model.</param>
    /// <returns>The updated AI model.</returns>
    [HttpPut("ai-models/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelInfo>> UpdateAiModel(Guid id, [FromBody] AiModelInfo aiModelInfo)
    {
        if (aiModelInfo == null || id != aiModelInfo.Id)
        {
            return BadRequest("Invalid AI model info or ID mismatch.");
        }

        var aiModel = await _dbContext.AiModels.FindAsync(id);
        if (aiModel == null)
        {
            return NotFound($"AI model with ID {id} not found.");
        }

        _mapper.Map(aiModelInfo, aiModel);
        await _dbContext.SaveChangesAsync();

        aiModelInfo = _mapper.Map<AiModelInfo>(aiModel);
        return Ok(aiModelInfo);
    }

    /// <summary>
    /// Deletes an AI model.
    /// </summary>
    /// <param name="id">The ID of the AI model to delete.</param>
    /// <returns>No content.</returns>
    [HttpDelete("ai-models/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteAiModel(Guid id)
    {
        // Check if model has deployments
        var hasDeployments = await _dbContext.AiModelDeployments.AnyAsync(d => d.AiModelId == id);
        if (hasDeployments)
        {
            return BadRequest("Cannot delete AI model that has deployments. Delete all deployments first.");
        }

        var aiModel = await _dbContext.AiModels.FindAsync(id);
        if (aiModel == null)
        {
            return NotFound($"AI model with ID {id} not found.");
        }

        _dbContext.AiModels.Remove(aiModel);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Gets all AI model deployments.
    /// </summary>
    /// <returns>A list of AI model deployment infos.</returns>
    [HttpGet("ai-model-deployments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<AiModelDeploymentInfo>>> GetAiModelDeployments()
    {
        var aiModelDeployments = await _dbContext.AiModelDeployments.ToListAsync();
        var aiModelDeploymentInfos = _mapper.Map<List<AiModelDeploymentInfo>>(aiModelDeployments);
        return Ok(aiModelDeploymentInfos);
    }

    /// <summary>
    /// Gets an AI model deployment by ID.
    /// </summary>
    /// <param name="id">The ID of the AI model deployment to retrieve.</param>
    /// <returns>The AI model deployment info.</returns>
    [HttpGet("ai-model-deployments/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelDeploymentInfo>> GetAiModelDeploymentById(Guid id)
    {
        var aiModelDeployment = await _dbContext.AiModelDeployments.FindAsync(id);
        if (aiModelDeployment == null)
        {
            return NotFound($"AI model deployment with ID {id} not found.");
        }

        var aiModelDeploymentInfo = _mapper.Map<AiModelDeploymentInfo>(aiModelDeployment);
        return Ok(aiModelDeploymentInfo);
    }

    /// <summary>
    /// Creates a new AI model deployment.
    /// </summary>
    /// <param name="aiModelDeploymentInfo">The AI model deployment to create.</param>
    /// <returns>The created AI model deployment.</returns>
    [HttpPost("ai-model-deployments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelDeploymentInfo>> CreateAiModelDeployment([FromBody] AiModelDeploymentInfo aiModelDeploymentInfo)
    {
        if (aiModelDeploymentInfo == null)
        {
            return BadRequest("AI model deployment info is required.");
        }

        // Check if the associated AI model exists
        var aiModel = await _dbContext.AiModels.FindAsync(aiModelDeploymentInfo.AiModelId);
        if (aiModel == null)
        {
            return NotFound($"AI model with ID {aiModelDeploymentInfo.AiModelId} not found.");
        }

        var aiModelDeployment = _mapper.Map<AiModelDeployment>(aiModelDeploymentInfo);
        _dbContext.AiModelDeployments.Add(aiModelDeployment);
        await _dbContext.SaveChangesAsync();

        aiModelDeploymentInfo = _mapper.Map<AiModelDeploymentInfo>(aiModelDeployment);
        return CreatedAtAction(nameof(GetAiModelDeploymentById), new { id = aiModelDeployment.Id }, aiModelDeploymentInfo);
    }

    /// <summary>
    /// Updates an existing AI model deployment.
    /// </summary>
    /// <param name="id">The ID of the AI model deployment to update.</param>
    /// <param name="aiModelDeploymentInfo">The updated AI model deployment.</param>
    /// <returns>The updated AI model deployment.</returns>
    [HttpPut("ai-model-deployments/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<AiModelDeploymentInfo>> UpdateAiModelDeployment(Guid id, [FromBody] AiModelDeploymentInfo aiModelDeploymentInfo)
    {
        if (aiModelDeploymentInfo == null || id != aiModelDeploymentInfo.Id)
        {
            return BadRequest("Invalid AI model deployment info or ID mismatch.");
        }

        var aiModelDeployment = await _dbContext.AiModelDeployments.FindAsync(id);
        if (aiModelDeployment == null)
        {
            return NotFound($"AI model deployment with ID {id} not found.");
        }

        _mapper.Map(aiModelDeploymentInfo, aiModelDeployment);
        await _dbContext.SaveChangesAsync();

        aiModelDeploymentInfo = _mapper.Map<AiModelDeploymentInfo>(aiModelDeployment);
        return Ok(aiModelDeploymentInfo);
    }

    /// <summary>
    /// Deletes an AI model deployment.
    /// </summary>
    /// <param name="id">The ID of the AI model deployment to delete.</param>
    /// <returns>No content.</returns>
    [HttpDelete("ai-model-deployments/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAiModelDeployment(Guid id)
    {
        var aiModelDeployment = await _dbContext.AiModelDeployments.FindAsync(id);
        if (aiModelDeployment == null)
        {
            return NotFound($"AI model deployment with ID {id} not found.");
        }

        _dbContext.AiModelDeployments.Remove(aiModelDeployment);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Starts an export job for a given index table (Postgres only).
    /// </summary>
    [HttpPost("indexes/export")]
    public async Task<IActionResult> StartIndexExport([FromBody] IndexExportRequest request)
    {
        if (!_serviceConfigurationOptionsMonitor.CurrentValue.GreenlightServices.Global.UsePostgresMemory)
            return BadRequest("Export is only available when UsePostgresMemory is enabled.");
        if (string.IsNullOrWhiteSpace(request.TableName) || string.IsNullOrWhiteSpace(request.Schema))
            return BadRequest("TableName and Schema are required.");
        var jobId = Guid.NewGuid();
        var grain = _clusterClient.GetGrain<IIndexExportGrain>(jobId);
        var userGroup = User?.Identity?.Name ?? "admin";
        _= grain.StartExportAsync(request.Schema, request.TableName, userGroup);
        return Accepted(new IndexJobStartedResponse { JobId = jobId });
    }

    /// <summary>
    /// Gets the status of an export job.
    /// </summary>
    [HttpGet("indexes/export/{jobId}")]
    public async Task<ActionResult<IndexExportJobStatus>> GetIndexExportStatus(Guid jobId)
    {
        var grain = _clusterClient.GetGrain<IIndexExportGrain>(jobId);
        var status = await grain.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Starts an import job for a given index table (Postgres only).
    /// </summary>
    [HttpPost("indexes/import")]
    public async Task<IActionResult> StartIndexImport([FromBody] IndexImportRequest request)
    {
        if (!_serviceConfigurationOptionsMonitor.CurrentValue.GreenlightServices.Global.UsePostgresMemory)
            return BadRequest("Import is only available when UsePostgresMemory is enabled.");
        if (string.IsNullOrWhiteSpace(request.TableName) || string.IsNullOrWhiteSpace(request.Schema) || string.IsNullOrWhiteSpace(request.BlobUrl))
            return BadRequest("TableName, Schema, and BlobUrl are required.");
        var jobId = Guid.NewGuid();
        var grain = _clusterClient.GetGrain<IIndexImportGrain>(jobId);
        var userGroup = User?.Identity?.Name ?? "admin";
        _ = grain.StartImportAsync(request.Schema, request.TableName, request.BlobUrl, userGroup);
        return Accepted(new IndexJobStartedResponse { JobId = jobId });
    }

    /// <summary>
    /// Gets the status of an import job.
    /// </summary>
    [HttpGet("indexes/import/{jobId}")]
    public async Task<ActionResult<IndexImportJobStatus>> GetIndexImportStatus(Guid jobId)
    {
        var grain = _clusterClient.GetGrain<IIndexImportGrain>(jobId);
        var status = await grain.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Checks if a given key matches any of the accepted keys with wildcard patterns.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is accepted, otherwise false.</returns>
    private bool IsAcceptedKey(string key)
    {
        foreach (var pattern in AcceptedKeys)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", @"[^:]*") + "$";

            if (Regex.IsMatch(key, regexPattern))
            {
                return true;
            }
        }

        return false;
    }
}


