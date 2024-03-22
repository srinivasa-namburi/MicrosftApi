using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Plugins.Default.NuclearDocs;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Generation;

public class USNuclearLicensingSemanticKernelBodyTextGenerator : IBodyTextGenerator
{
    private readonly Kernel _semanticKernel;
    private readonly ILogger<USNuclearLicensingSemanticKernelBodyTextGenerator> _logger;

    public USNuclearLicensingSemanticKernelBodyTextGenerator(Kernel semanticKernel, ILogger<USNuclearLicensingSemanticKernelBodyTextGenerator> logger)
    {
        _semanticKernel = semanticKernel;
        _logger = logger;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber,
        string sectionTitle, string tableOfContentsString, Guid? metadataId)
    {
        _semanticKernel.Plugins.TryGetFunction(pluginName: ("native_" + nameof(NRCDocumentsPlugin)),
            functionName: "GetBodyTextNodesOnly", out KernelFunction? function);

        var functionParameters = new Dictionary<string, object>
        {
            { "sectionOrTitleNumber", sectionNumber },
            { "sectionOrTitleText", sectionTitle },
            { "contentNodeTypeString", contentNodeType },
            { "tableOfContentsString", tableOfContentsString }
        };

        if (metadataId.HasValue)
        {
            functionParameters.Add("metadataId", metadataId.Value);
        }
        
        var result = await _semanticKernel.InvokeAsync<List<ContentNode>>(
            "native_"+nameof(NRCDocumentsPlugin),
            "GetBodyTextNodesOnly", // "GetBodyTextNodesOnly" is the name of the function in the "NuclearDocumentRepositoryPlugin"
            new KernelArguments(functionParameters!)
            );

        return result;
    }
}        