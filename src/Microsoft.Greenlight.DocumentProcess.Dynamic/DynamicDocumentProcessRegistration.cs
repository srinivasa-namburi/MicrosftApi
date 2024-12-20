using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Dynamic.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.DocumentProcess.Dynamic;

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

        var documentProcesses = dbContext.DynamicDocumentProcessDefinitions
            .Where(x=>x.LogicType == DocumentProcessLogicType.KernelMemory)
            .Include(x => x.DocumentOutline)
            .ToList();

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

            if (options.GreenlightServices.DocumentGeneration.CreateBodyTextNodes)
            {
                builder.Services.AddKeyedScoped<IBodyTextGenerator>(documentProcess.ShortName + "-IBodyTextGenerator", (sp, a) =>
                    new KernelMemoryBodyTextGenerator(
                        sp.GetRequiredKeyedService<IAiCompletionService>(documentProcess.ShortName + "-IAiCompletionService"),
                        sp.GetRequiredService<IConsolidatedSearchOptionsFactory>(),
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
