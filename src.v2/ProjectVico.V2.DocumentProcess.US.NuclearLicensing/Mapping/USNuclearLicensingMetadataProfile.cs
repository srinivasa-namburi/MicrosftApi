using AutoMapper;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Models;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Mapping;

public class USNuclearLicensingMetadataProfile : Profile
{
    public USNuclearLicensingMetadataProfile()
    {
        CreateMap<IDocumentGenerationRequest, USNuclearEnvironmentalReportMetadata>();

        CreateMap<DocumentGenerationRequest, USNuclearEnvironmentalReportMetadata>()
         .IncludeBase<IDocumentGenerationRequest, USNuclearEnvironmentalReportMetadata>();
    }
}