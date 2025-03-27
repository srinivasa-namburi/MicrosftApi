using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Configuration;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping between <see cref="DbConfiguration"/> and <see cref="DbConfigurationInfo"/>.
    /// </summary>
    public class DbConfigurationInfoProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DbConfigurationInfoProfile"/> class.
        /// Defines the mapping between <see cref="DbConfiguration"/> and <see cref="DbConfigurationInfo"/>.
        /// </summary>
        public DbConfigurationInfoProfile()
        {
            CreateMap<DbConfiguration, DbConfigurationInfo>();
        }
    }
}