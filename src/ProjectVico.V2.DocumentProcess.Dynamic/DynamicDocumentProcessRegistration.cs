using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Dynamic.Generation;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.DocumentProcess.Dynamic;

public class DynamicDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "Dynamic";
    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();

        // Services registered individually for ALL dynamic document processes (resolved by document process name as opposed to a single-use Dynamic registration)
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        var documentProcesses = dbContext.DynamicDocumentProcessDefinitions.Where(x=>x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();
        foreach (var documentProcessDefinition in documentProcesses)
        {
            var documentProcess = mapper.Map<DocumentProcessInfo>(documentProcessDefinition);

            builder.AddKeyedKernelMemoryForDocumentProcess(options, documentProcess);
            builder.Services.AddKeyedScoped<IKernelMemoryRepository, KernelMemoryRepository>(
                documentProcess.ShortName + "-IKernelMemoryRepository");
            
            builder.Services.AddKeyedScoped<IAiCompletionService>(documentProcess.ShortName + "-IAiCompletionService", (sp, a) =>
                new GenericAiCompletionService(
                    sp.GetRequiredService<AiCompletionServiceParameters<GenericAiCompletionService>>(),
                    documentProcess.ShortName
                ));

            if (options.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
            {
                builder.Services.AddKeyedScoped<IBodyTextGenerator>(documentProcess.ShortName + "-IBodyTextGenerator", (sp, a) =>
                    new KernelMemoryBodyTextGenerator(
                        sp.GetRequiredKeyedService<IAiCompletionService>(documentProcess.ShortName + "-IAiCompletionService"),
                        sp.GetRequiredService<ILogger<KernelMemoryBodyTextGenerator>>(),
                        sp.GetRequiredService<IServiceProvider>()
                    ));
            }
        }
        // END Dynamic services
        
        // Shared Dynamic Services that are resolved via "Dynamic" - this is the fallback when a service for a specific document process is not found
        builder.Services.AddKeyedScoped<IDocumentOutlineService, DynamicDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // End Shared Dynamic Services

        return builder;
    }
}