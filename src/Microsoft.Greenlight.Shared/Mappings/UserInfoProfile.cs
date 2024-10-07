using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

public class UserInfoProfile : Profile
{
    public UserInfoProfile()
    {
        CreateMap<UserInfoDTO, UserInformation>();

        CreateMap<UserInformation, UserInfoDTO>();
    }
}
