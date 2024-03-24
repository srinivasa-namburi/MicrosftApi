using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.API.Main.Controllers;

public class AuthorizationController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;

    public AuthorizationController(
        DocGenerationDbContext dbContext,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

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