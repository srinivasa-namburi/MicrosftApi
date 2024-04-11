using System.Reflection;
using AutoMapper;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public class MetadataService : IMetadataService
{
    private readonly IMapper _mapper;

    public MetadataService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public MetadataDefinition CreateMetadataModelFromSource(IDocumentGenerationRequest documentGenerationRequest)
    {
        
        var resultantDefinitionShortName = documentGenerationRequest.MetadataModelName;
        var resultantDefinitionFullTypeName = "ProjectVico.V2.DocumentProcess." + documentGenerationRequest.DocumentProcessName + ".Models." +
                           resultantDefinitionShortName;

        var resultantDefinitionAssemblyName = "ProjectVico.V2.DocumentProcess." + documentGenerationRequest.DocumentProcessName;
        var resultantDefinitionAssembly = Assembly.Load(resultantDefinitionAssemblyName);

        var resultantDefinitionType = resultantDefinitionAssembly.GetType(resultantDefinitionFullTypeName);

        var documentGenerationRequestAssemblyName = "ProjectVico.V2.Shared";
        var documentGenerationRequestAssembly = Assembly.Load(documentGenerationRequestAssemblyName);
        var documentGenerationRequestType = documentGenerationRequestAssembly.GetType(documentGenerationRequest.DocumentGenerationRequestFullTypeName);
        
        
        var result = _mapper.Map(documentGenerationRequest, documentGenerationRequestType, resultantDefinitionType);
        return (MetadataDefinition)result;
    }

    public IDocumentGenerationRequest CreateMetadataFromModel(MetadataDefinition metadataModel)
    {
        throw new NotImplementedException();
    }
}