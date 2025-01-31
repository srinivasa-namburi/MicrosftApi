using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="UserInfoDTO"/> and <see cref="UserInformation"/>.
/// </summary>
public class UserInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoProfile"/> class.
    /// Defining the mapping between <see cref="UserInfoDTO"/> and <see cref="UserInformation"/>.
    /// </summary>
    public UserInfoProfile()
    {
        CreateMap<UserInfoDTO, UserInformation>()
            .ReverseMap();
    }
}
