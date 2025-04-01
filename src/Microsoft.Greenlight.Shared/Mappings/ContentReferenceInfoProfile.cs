using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping between <see cref="ContentReferenceItem"/> and <see cref="ContentReferenceItemInfo"/>.
    /// </summary>
    public class ContentReferenceInfoProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentReferenceInfoProfile"/> class.
        /// Defines the mapping between <see cref="ContentReferenceItem"/> and <see cref="ContentReferenceItemInfo"/>.
        /// The <see cref="ContentReferenceItemInfo"/> does not have the RagText property.
        /// </summary>
        public ContentReferenceInfoProfile()
        {
            CreateMap<ContentReferenceItem, ContentReferenceItemInfo>()
                .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => (DateTime?)src.CreatedUtc))
                .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => (DateTime?)src.ModifiedUtc));

            CreateMap<ContentReferenceItemInfo, ContentReferenceItem>()
                .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreatedUtc ?? DateTime.UtcNow))
                .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => src.ModifiedUtc ?? DateTime.UtcNow))
                .ForMember(dest => dest.RagText, opt => opt.Ignore())
                .ForMember(dest => dest.Embeddings, opt => opt.Ignore());
        }
    }
}