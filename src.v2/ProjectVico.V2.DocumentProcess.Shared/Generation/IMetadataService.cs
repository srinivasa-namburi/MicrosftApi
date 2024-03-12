using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public interface IMetadataService
{
    MetadataDefinition CreateMetadataModelFromSource (IDocumentGenerationRequest documentGenerationRequest);
    IDocumentGenerationRequest CreateMetadataFromModel (MetadataDefinition metadataModel);
}