using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling authorization-related operations.
/// </summary>
public class AuthorizationController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    public AuthorizationController(
        DocGenerationDbContext dbContext,
        IMapper mapper
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
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
    [AllowAnonymous]
    public async Task<IActionResult> StoreOrUpdateUserDetails([FromBody] UserInfoDTO? userInfoDto)
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
    /// Gets user information by provider subject ID.
    /// </summary>
    /// <param name="providerSubjectId">The provider subject ID.</param>
    /// <returns>An <see cref="ActionResult{UserInfoDTO}"/> containing the user information.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When a Provider Subject Id is not provided
    /// </returns>
    [HttpGet("{providerSubjectId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserInfoDTO>> GetUserInfo(String providerSubjectId)
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
}
