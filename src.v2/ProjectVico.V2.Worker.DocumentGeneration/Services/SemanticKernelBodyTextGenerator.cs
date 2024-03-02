using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using ProjectVico.V2.Shared.Models;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using ProjectVico.V2.Plugins.NuclearDocs.NativePlugins;
using System.Numerics;

namespace ProjectVico.V2.Worker.DocumentGeneration.Services;

public class SemanticKernelBodyTextGenerator : IBodyTextGenerator
{
    private readonly Kernel _semanticKernel;
    private readonly ILogger<SemanticKernelBodyTextGenerator> _logger;

    public SemanticKernelBodyTextGenerator(Kernel semanticKernel, ILogger<SemanticKernelBodyTextGenerator> logger)
    {
        _semanticKernel = semanticKernel;
        _logger = logger;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber,
        string sectionTitle)
    {
        // var planRequest = $"""
        //                    Generate the boxy text nodes ONLY for this {contentNodeType} using the "GetBodyTextNodesOnly" plugin function
        //                    in the NuclearDocumentRepositoryPlugin,
        //                    using these settings for parameters:

        //                    sectionOrTitleNumber = "{sectionNumber}"
        //                    sectionOrTitleText = "{sectionTitle}",
        //                    contentNodeTypeString = "{contentNodeType}"
        //                    """;

        //var plan =
        //    await new HandlebarsPlanner(new HandlebarsPlannerOptions(allowLoops: false)).CreatePlanAsync(
        //        _semanticKernel,
        //        planRequest);

        //var bodyContentNodesJson = await plan.InvokeAsync(_semanticKernel);
        //using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyContentNodesJson));
        //var bodyContentNodes = await JsonSerializer.DeserializeAsync<List<ContentNode>>(memoryStream);
        //return bodyContentNodes;

        _semanticKernel.Plugins.TryGetFunction(pluginName: ("native_" + nameof(NuclearDocumentRepositoryPlugin)),
            functionName: "GetBodyTextNodesOnly", out KernelFunction? function);

        var functionParameters = new Dictionary<string, object>
        {
            { "sectionOrTitleNumber", sectionNumber },
            { "sectionOrTitleText", sectionTitle },
            { "contentNodeTypeString", contentNodeType }
        };
        
        var result = await _semanticKernel.InvokeAsync<List<ContentNode>>(
            "native_"+nameof(NuclearDocumentRepositoryPlugin),
            "GetBodyTextNodesOnly", // "GetBodyTextNodesOnly" is the name of the function in the "NuclearDocumentRepositoryPlugin"
            new KernelArguments(functionParameters!)
            );

        return result;
    }
}        