using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;
using Microsoft.Greenlight.Shared.Models.Authorization;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// AutoMapper profile for authorization entities and DTOs.
/// </summary>
public sealed class AuthorizationProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationProfile"/> class.
    /// </summary>
    public AuthorizationProfile()
    {
        CreateMap<GreenlightPermission, PermissionInfo>();
        
        CreateMap<GreenlightRole, RoleInfo>()
            .ForMember(dest => dest.PermissionIds, opt => opt.MapFrom(src => src.RolePermissions.Select(rp => rp.PermissionId).ToList()));
        
        CreateMap<GreenlightUserRole, UserRoleAssignmentInfo>();
        
        CreateMap<GreenlightUserRole, UserSearchRoleAssignment>()
            .ForMember(dest => dest.RoleName, opt => opt.Ignore()); // Will be populated separately in the controller
    }
}