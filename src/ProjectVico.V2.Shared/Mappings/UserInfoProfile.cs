using AutoMapper;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Mappings;

public class UserInfoProfile : Profile
{
    public UserInfoProfile()
    {
        CreateMap<UserInfoDTO, UserInformation>();

        CreateMap<UserInformation, UserInfoDTO>();
    }
}