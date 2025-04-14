using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping between validation pipeline entities and their DTOs.
    /// </summary>
    public class ValidationInfoProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfoProfile"/> class.
        /// Defines the mappings between validation-related entities and their DTOs.
        /// </summary>
        public ValidationInfoProfile()
        {
            // Map DocumentProcessValidationPipeline to DocumentProcessValidationPipelineInfo
            CreateMap<DocumentProcessValidationPipeline, DocumentProcessValidationPipelineInfo>()
                .ForMember(dest => dest.ValidationPipelineSteps, opt => opt.MapFrom(src => 
                    src.ValidationPipelineSteps.OrderBy(s => s.Order)));
            
            CreateMap<DocumentProcessValidationPipelineInfo, DocumentProcessValidationPipeline>()
                .ForMember(dest => dest.ValidationPipelineExecutions, opt => opt.Ignore())
                .ForMember(dest => dest.DocumentProcess, opt => opt.Ignore());

            // Map DocumentProcessValidationPipelineStep to DocumentProcessValidationPipelineStepInfo
            CreateMap<DocumentProcessValidationPipelineStep, DocumentProcessValidationPipelineStepInfo>();
            CreateMap<DocumentProcessValidationPipelineStepInfo, DocumentProcessValidationPipelineStep>()
                .ForMember(dest => dest.DocumentProcessValidationPipeline, opt => opt.Ignore());

            // Map ValidationExecutionStepContentNodeResult to ValidationExecutionStepContentNodeResultInfo
            CreateMap<ValidationExecutionStepContentNodeResult, ValidationExecutionStepContentNodeResultInfo>()
                .ReverseMap();

        }
    }
}