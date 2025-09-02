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
        ILogger<AuthorizationController> logger
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _clusterClient = clusterClient;
        _logger = logger;
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

        var grain = _clusterClient.GetGrain<IUserTokenStoreGrain>(callerSub);
        await grain.SetTokenAsync(payload);
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
