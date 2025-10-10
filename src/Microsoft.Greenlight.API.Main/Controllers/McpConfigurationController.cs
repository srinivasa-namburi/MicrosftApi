using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Web.Shared.ViewModels.MCP;
using Orleans.Streams;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Admin endpoints for MCP configuration and session management.
/// Uses the EF-backed configuration provider for persistence and broadcasting updates.
/// </summary>
[ApiController]
[Route("api/mcp-config")]
public class McpConfigurationController : BaseController
{
    private readonly IConfiguration _configuration;
    private readonly EfCoreConfigurationProvider _configProvider;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<McpConfigurationController> _logger;

    public McpConfigurationController(
        IConfiguration configuration,
        EfCoreConfigurationProvider configProvider,
        DocGenerationDbContext dbContext,
        IClusterClient clusterClient,
        ILogger<McpConfigurationController> logger)
    {
        _configuration = configuration;
        _configProvider = configProvider;
        _dbContext = dbContext;
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<McpConfigurationModel> Get()
    {
        // Read from IConfiguration to reflect persisted values reliably
        var mcpOptions = _configuration
            .GetSection("ServiceConfiguration:Mcp")
            .Get<ServiceConfigurationOptions.McpOptions>()
            ?? new ServiceConfigurationOptions.McpOptions();

        var model = new McpConfigurationModel
        {
            Common = new CommonSection
            {
                DisableAuth = mcpOptions.DisableAuth,
                SecretEnabled = mcpOptions.SecretEnabled
            }
        };

        return Ok(model);
    }

    [HttpPut]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] McpConfigurationModel model)
    {
        if (model is null)
        {
            return BadRequest("Model is required");
        }

        // Load or create config document
        var config = await _dbContext.Configurations.FindAsync(Microsoft.Greenlight.Shared.Models.Configuration.DbConfiguration.DefaultId);
        if (config is null)
        {
            config = new Microsoft.Greenlight.Shared.Models.Configuration.DbConfiguration
            {
                ConfigurationValues = "{}",
                LastUpdated = DateTime.UtcNow,
                LastUpdatedBy = "System"
            };
            _dbContext.Configurations.Add(config);
        }

        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(config.ConfigurationValues)
                   ?? new Dictionary<string, string>();

        // Build updates with defaults
        var updates = new Dictionary<string, string>
        {
            ["ServiceConfiguration:Mcp:DisableAuth"] = model.Common.DisableAuth.ToString(),
            ["ServiceConfiguration:Mcp:SecretEnabled"] = model.Common.SecretEnabled.ToString(),
            // Always use default header name
            ["ServiceConfiguration:Mcp:SecretHeaderName"] = "X-MCP-Secret"
        };

        foreach (var kvp in updates)
        {
            dict[kvp.Key] = kvp.Value;
        }

        // Persist configuration changes
        config.ConfigurationValues = System.Text.Json.JsonSerializer.Serialize(dict);
        config.LastUpdated = DateTime.UtcNow;
        config.LastUpdatedBy = "System";
        await _dbContext.SaveChangesAsync();

        // Update runtime configuration
        _configProvider.UpdateOptions(updates);

        // Broadcast configuration update to all services
        var configurationUpdatedMessage = new ConfigurationUpdated(Guid.NewGuid());
        var streamProvider = _clusterClient.GetStreamProvider("StreamProvider");
        var stream = streamProvider.GetStream<ConfigurationUpdated>(
            SystemStreamNameSpaces.ConfigurationUpdatedNamespace,
            Guid.Empty);
        await stream.OnNextAsync(configurationUpdatedMessage);

        return NoContent();
    }

    // Secrets management
    [HttpGet("secrets")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult<List<McpSecretInfo>> ListSecrets([FromServices] AutoMapper.IMapper mapper)
    {
        var list = _dbContext.Set<Microsoft.Greenlight.Shared.Models.Configuration.McpSecret>()
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
            .ToList();
        return Ok(mapper.Map<List<McpSecretInfo>>(list));
    }

    [HttpPost("secrets")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<CreateMcpSecretResponse>> CreateSecret(
        [FromBody] CreateMcpSecretRequest request,
        [FromServices] Microsoft.Greenlight.Shared.Services.Security.ISecretHashingService hashing,
        [FromServices] AutoMapper.IMapper mapper)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ProviderSubjectId))
        {
            return BadRequest("Name and ProviderSubjectId are required");
        }
        var name = request.Name.Trim();
        if (name.Length > 128)
        {
            return BadRequest("Name too long");
        }
        if (_dbContext.Set<Microsoft.Greenlight.Shared.Models.Configuration.McpSecret>().Any(s => s.Name == name))
        {
            return BadRequest($"A secret named '{name}' already exists");
        }
        // Generate a random plaintext once
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(bytes);
        var (salt, hash) = hashing.Hash(plaintext);
        var entity = new Microsoft.Greenlight.Shared.Models.Configuration.McpSecret
        {
            Name = name,
            ProviderSubjectId = request.ProviderSubjectId.Trim(),
            SecretSalt = salt,
            SecretHash = hash,
            IsActive = true
        };
        _dbContext.Add(entity);
        await _dbContext.SaveChangesAsync();
        var info = mapper.Map<McpSecretInfo>(entity);
        return Ok(new CreateMcpSecretResponse { Secret = info, Plaintext = plaintext });
    }

    [HttpDelete("secrets/{id:guid}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSecret(Guid id)
    {
        var entity = await _dbContext.Set<Microsoft.Greenlight.Shared.Models.Configuration.McpSecret>().FindAsync(id);
        if (entity == null) { return NoContent(); }
        _dbContext.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private bool GetBool(string key)
    {
        return bool.TryParse(_configuration[key], out var b) && b;
    }
}
