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
        var model = new McpConfigurationModel
        {
            Common = new CommonSection
            {
                DisableAuth = GetBool("ServiceConfiguration:Mcp:DisableAuth"),
                SecretEnabled = GetBool("ServiceConfiguration:Mcp:SecretEnabled"),
                SecretHeaderName = _configuration["ServiceConfiguration:Mcp:SecretHeaderName"]
            },
            Core = new EndpointSection
            {
                SecretEnabled = GetBool("ServiceConfiguration:Mcp:Core:SecretEnabled"),
                SecretHeaderName = _configuration["ServiceConfiguration:Mcp:Core:SecretHeaderName"]
            },
            Flow = new EndpointSection
            {
                SecretEnabled = GetBool("ServiceConfiguration:Mcp:Flow:SecretEnabled"),
                SecretHeaderName = _configuration["ServiceConfiguration:Mcp:Flow:SecretHeaderName"]
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

        // Server-side validation
        var errors = new List<string>();

        static bool IsValidHeaderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length > 100) return false;
            // RFC7230 tchar set simplified
            const string pattern = @"^[!#$%&'*+.^_`|~0-9A-Za-z-]+$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, pattern);
        }

        // Normalize and validate header names where required
        model.Common.SecretHeaderName = model.Common.SecretHeaderName?.Trim();
        model.Core.SecretHeaderName = model.Core.SecretHeaderName?.Trim();
        model.Flow.SecretHeaderName = model.Flow.SecretHeaderName?.Trim();

        if (model.Common.SecretEnabled && !IsValidHeaderName(model.Common.SecretHeaderName))
        {
            errors.Add("Common: Secret is enabled, but Secret Header Name is invalid or missing.");
        }

        if (model.Core.SecretEnabled && !IsValidHeaderName(model.Core.SecretHeaderName ?? model.Common.SecretHeaderName))
        {
            errors.Add("Core: Secret is enabled, but no valid header name (Core or Common) is provided.");
        }

        if (model.Flow.SecretEnabled && !IsValidHeaderName(model.Flow.SecretHeaderName ?? model.Common.SecretHeaderName))
        {
            errors.Add("Flow: Secret is enabled, but no valid header name (Flow or Common) is provided.");
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

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

        var updates = new Dictionary<string, string>();
        updates["ServiceConfiguration:Mcp:DisableAuth"] = model.Common.DisableAuth.ToString();
        updates["ServiceConfiguration:Mcp:SecretEnabled"] = model.Common.SecretEnabled.ToString();
        if (!string.IsNullOrWhiteSpace(model.Common.SecretHeaderName))
        {
            updates["ServiceConfiguration:Mcp:SecretHeaderName"] = model.Common.SecretHeaderName!;
        }
        // optional endpoint-specific settings
        updates["ServiceConfiguration:Mcp:Core:SecretEnabled"] = model.Core.SecretEnabled.ToString();
        if (!string.IsNullOrWhiteSpace(model.Core.SecretHeaderName))
        {
            updates["ServiceConfiguration:Mcp:Core:SecretHeaderName"] = model.Core.SecretHeaderName!;
        }
        updates["ServiceConfiguration:Mcp:Flow:SecretEnabled"] = model.Flow.SecretEnabled.ToString();
        if (!string.IsNullOrWhiteSpace(model.Flow.SecretHeaderName))
        {
            updates["ServiceConfiguration:Mcp:Flow:SecretHeaderName"] = model.Flow.SecretHeaderName!;
        }

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

        // Broadcast
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
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.UserOid))
        {
            return BadRequest("Name and UserOid are required");
        }
        if (!Guid.TryParse(request.UserOid, out _))
        {
            return BadRequest("UserOid must be a valid GUID");
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
            UserOid = request.UserOid.Trim(),
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

    [HttpGet("sessions")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<McpSessionRow>>> ListSessions([FromServices] Microsoft.Greenlight.Shared.Services.Caching.IAppCache cache)
    {
        var index = await LoadIndexAsync(cache, HttpContext.RequestAborted);
        var rows = new List<McpSessionRow>();
        foreach (var id in index)
        {
            var s = await cache.GetOrCreateAsync<McpSessionMirror>(
                GetKey(id), _ => Task.FromResult<McpSessionMirror?>(null!)!, TimeSpan.FromMinutes(30), allowDistributed: true, HttpContext.RequestAborted);
            if (s != null && s.SessionId != Guid.Empty)
            {
                rows.Add(new McpSessionRow { SessionId = s.SessionId, ExpiresUtc = s.ExpiresUtc });
            }
        }
        rows = rows.OrderBy(r => r.ExpiresUtc).ToList();
        return Ok(rows);
    }

    [HttpDelete("sessions/{id:guid}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InvalidateSession(Guid id, [FromServices] Microsoft.Greenlight.Shared.Services.Caching.IAppCache cache)
    {
        // remove session and update index
        await cache.RemoveAsync(GetKey(id), HttpContext.RequestAborted);
        var index = await LoadIndexAsync(cache, HttpContext.RequestAborted);
        if (index.Remove(id))
        {
            await SaveIndexAsync(cache, index, HttpContext.RequestAborted);
        }
        return NoContent();
    }

    private bool GetBool(string key)
    {
        return bool.TryParse(_configuration[key], out var b) && b;
    }

    private static string GetKey(Guid id) => $"mcp:sessions:{id}";
    private const string IndexKey = "mcp:sessions:index";
    private static async Task<HashSet<Guid>> LoadIndexAsync(Microsoft.Greenlight.Shared.Services.Caching.IAppCache cache, CancellationToken ct)
    {
        var set = await cache.GetOrCreateAsync<HashSet<Guid>>(IndexKey, _ => Task.FromResult(new HashSet<Guid>()), TimeSpan.FromHours(12), allowDistributed: true, ct);
        return set ?? new HashSet<Guid>();
    }
    private static Task SaveIndexAsync(Microsoft.Greenlight.Shared.Services.Caching.IAppCache cache, HashSet<Guid> index, CancellationToken ct)
        => cache.SetAsync(IndexKey, index, TimeSpan.FromHours(12), allowDistributed: true, ct);

    private sealed class McpSessionMirror
    {
        public Guid SessionId { get; set; }
        public string UserObjectId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }
}
