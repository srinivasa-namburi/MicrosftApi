using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling authorization-related operations.
/// </summary>
[Route("/api/authorization")]
public class AuthorizationController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IClusterClient _clusterClient;
    private readonly ICachedPermissionService _cachedPermissionService;
    private readonly HashSet<string> _trustedCallerClientIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AuthorizationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="clusterClient">Orleans cluster client.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthorizationController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IClusterClient clusterClient,
        ILogger<AuthorizationController> logger,
        ICachedPermissionService cachedPermissionService,
        IConfiguration configuration
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _clusterClient = clusterClient;
        _logger = logger;
        _cachedPermissionService = cachedPermissionService;

        // Load trusted caller client IDs (Entra app client IDs) from configuration if provided.
        // Comma or space separated list under AzureAd:TrustedCallerClientIds
        var raw = configuration["AzureAd:TrustedCallerClientIds"];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var part in raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _trustedCallerClientIds.Add(part);
            }
            if (_trustedCallerClientIds.Count > 0)
            {
                _logger.LogInformation("AuthorizationController configured with {Count} trusted caller client IDs", _trustedCallerClientIds.Count);
            }
        }

        // Final fallback: if not configured, accept our own ClientId by default
        if (_trustedCallerClientIds.Count == 0)
        {
            var selfClientId = configuration["AzureAd:ClientId"];
            if (!string.IsNullOrWhiteSpace(selfClientId))
            {
                _trustedCallerClientIds.Add(selfClientId);
                _logger.LogInformation("AuthorizationController defaulted TrustedCallerClientIds to its own ClientId");
            }
        }
    }

    /// <summary>
    /// Stores or updates user details.
    /// </summary>
    /// <param name="userInfoDto">The user information DTO.</param>
    /// <returns>An <see cref="IActionResult"/> representing the result of the operation.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When User Info is missing
    /// </returns>
    [HttpPost("store-or-update-user-details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<ActionResult<UserInfoDTO>> StoreOrUpdateUserDetails([FromBody] UserInfoDTO? userInfoDto)
    {
        if (userInfoDto == null)
        {
            return BadRequest();
        }

        var userInformation = await _dbContext.UserInformations
            .FirstOrDefaultAsync(x => x.ProviderSubjectId == userInfoDto.ProviderSubjectId);

        if (userInformation == null)
        {
            userInformation = _mapper.Map<UserInformation>(userInfoDto);
            _dbContext.UserInformations.Add(userInformation);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            var existingId = userInformation.Id;
            userInformation = _mapper.Map(userInfoDto, userInformation);
            userInformation.Id = existingId;

            _dbContext.UserInformations.Update(userInformation);
            await _dbContext.SaveChangesAsync();
        }

        userInfoDto = _mapper.Map<UserInfoDTO>(userInformation);

        return Ok(userInfoDto);
    }

    /// <summary>
    /// Upserts the user and synchronizes Entra roles to Greenlight assignments using provided token roles.
    /// Applies fallback FullAccess when token contains DocumentGeneration and no explicit mapping/assignment exists.
    /// </summary>
    /// <param name="request">The request containing user identity and token roles.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("first-login-sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Consumes("application/json")]
    [Authorize]
    public async Task<IActionResult> FirstLoginSync([FromBody] FirstLoginSyncRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProviderSubjectId) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Missing required fields for first login sync");
        }

        // Optional hardening: if configured, restrict to trusted callers by azp/appid claim
        if (_trustedCallerClientIds.Count > 0)
        {
            var clientId = HttpContext.User.FindFirst("azp")?.Value
                           ?? HttpContext.User.FindFirst("appid")?.Value;
            if (string.IsNullOrEmpty(clientId) || !_trustedCallerClientIds.Contains(clientId))
            {
                _logger.LogWarning("FirstLoginSync denied for untrusted caller clientId={ClientId}", clientId ?? "<null>");
                return Forbid();
            }
        }

        // Upsert user information
        var userInformation = await _dbContext.UserInformations
            .FirstOrDefaultAsync(x => x.ProviderSubjectId == request.ProviderSubjectId);

        if (userInformation == null)
        {
            userInformation = new UserInformation
            {
                ProviderSubjectId = request.ProviderSubjectId,
                FullName = request.FullName,
                Email = request.Email,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow
            };
            _dbContext.UserInformations.Add(userInformation);
        }
        else
        {
            userInformation.FullName = request.FullName;
            userInformation.Email = request.Email;
            userInformation.ModifiedUtc = DateTime.UtcNow;
            _dbContext.UserInformations.Update(userInformation);
        }
        await _dbContext.SaveChangesAsync();

        // Sync roles based on provided token role names/ids
        var tokenRoleNames = request.TokenRoleNames?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokenRoleIds = request.TokenRoleIds?.ToHashSet() ?? new HashSet<Guid>();

        try
        {
            await _cachedPermissionService.SyncUserRolesAndGetPermissionsAsync(
                request.ProviderSubjectId,
                tokenRoleNames,
                tokenRoleIds,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync roles during first login for {ProviderSubjectId}", request.ProviderSubjectId);
        }

        return NoContent();
    }

    /// <summary>
    /// Upserts the user's bearer access token used for downstream services (e.g., connected MCP servers).
    /// </summary>
    /// <param name="payload">The user token payload.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("user-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> SetUserToken([FromBody] UserTokenDTO payload)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return BadRequest("Invalid token payload");
        }
        // Normalize to the caller's current subject claim to avoid mismatches
        var callerSub = HttpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(callerSub))
        {
            // Fall back to payload's value but this should not normally occur in authenticated calls
            callerSub = payload.ProviderSubjectId;
        }
        if (string.IsNullOrWhiteSpace(callerSub))
        {
            return BadRequest("Missing ProviderSubjectId");
        }
        payload.ProviderSubjectId = callerSub;

        _logger.LogInformation("SetUserToken received for sub {Sub}. Token length={TokenLength}.", callerSub, payload.AccessToken?.Length ?? 0);

        // If this fails it usually means that the silo hasn't fully started yet.
        // Add a one-time 15-second retry loop to handle this scenario, retrying every 3 seconds.

        const int maxRetries = 5;
        var delayBetweenRetries = TimeSpan.FromSeconds(3);
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var grain = _clusterClient.GetGrain<IUserTokenStoreGrain>(callerSub);
                await grain.SetTokenAsync(payload);
                return NoContent();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("Attempt {Attempt} to set user token failed. Retrying in {Delay}...", attempt, delayBetweenRetries);
                await Task.Delay(delayBetweenRetries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All attempts to set user token failed.");
                throw; // Re-throw the exception after all retries have been exhausted
            }
        }
        return NoContent();
    }

    /// <summary>
    /// Gets user information by provider subject ID.
    /// </summary>
    /// <param name="providerSubjectId">The provider subject ID.</param>
    /// <returns>An <see cref="ActionResult{UserInfoDTO}"/> containing the user information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When a Provider Subject Id is not provided
    ///     404 Not Found: When a Provider Subject Id is not found
    /// </returns>
    [HttpGet("{providerSubjectId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<UserInfoDTO>> GetUserInfo(string providerSubjectId)
    {
        var userInformation = await _dbContext.UserInformations
            .FirstOrDefaultAsync(x => x.ProviderSubjectId == providerSubjectId);

        if (userInformation == null)
        {
            return NotFound();
        }

        var userInfoDto = _mapper.Map<UserInfoDTO>(userInformation);

        return Ok(userInfoDto);
    }

    /// <summary>
    /// Gets user theme preference information.
    /// </summary>
    /// <param name="providerSubjectId">The provider subject ID.</param>
    /// <returns>An <see cref="ActionResult{ThemePreference}"/> containing the user information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When a Provider Subject Id is not found
    /// </returns>
    [HttpGet("theme/{providerSubjectId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Consumes("application/text")]
    [Produces("application/json")]
    public async Task<ActionResult<ThemePreference>> GetThemePreference(string providerSubjectId)
    {
        var userInformation = await _dbContext.UserInformations
            .FirstOrDefaultAsync(x => x.ProviderSubjectId == providerSubjectId);

        if (userInformation == null)
        {
            return NotFound();
        }

        return Ok(userInformation.ThemePreference);
    }


    /// <summary>
    /// Sets the theme preference for the user.
    /// </summary>
    /// <param name="themePreferenceDto"></param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the result of the operation.
    /// Produces Status Codes:
    ///     204 No Content: When completed sucessfully
    ///     404 Not Found: When a Provider Subject Id is not found
    /// </returns>
    [HttpPost("theme")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Consumes("application/json")]
    public async Task<IActionResult> SetThemePreference([FromBody] ThemePreferenceDTO themePreferenceDto)
    {
        var userInformation = await _dbContext.UserInformations
            .FirstOrDefaultAsync(x => x.ProviderSubjectId == themePreferenceDto.ProviderSubjectId);

        if (userInformation == null)
        {
            return NotFound();
        }

        userInformation.ThemePreference = (ThemePreference)themePreferenceDto.ThemePreference!;
        _dbContext.UserInformations.Update(userInformation);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}
