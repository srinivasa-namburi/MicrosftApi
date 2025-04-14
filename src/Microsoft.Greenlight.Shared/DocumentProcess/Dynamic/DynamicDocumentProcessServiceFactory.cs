using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Dynamic
{
    /// <summary>
    /// This factory returns services for a particular document process, constructing them on demand.
    /// </summary>
    public class DynamicDocumentProcessServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, object?> _serviceCache = new();
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

        public DynamicDocumentProcessServiceFactory(
            IServiceProvider serviceProvider, 
            IDbContextFactory<DocGenerationDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            _serviceProvider = serviceProvider.CreateScope().ServiceProvider;
        }

        /// <summary>
        /// Return a service for a particular document process.
        /// Or null if the service is not registered.
        /// </summary>
        /// <param name="documentProcessName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetService<T>(string documentProcessName)
        {
            // Use the document process name as part of the cache key
            var cacheKey = $"{documentProcessName}-{typeof(T).Name}";

            // Retrieve or create the service
            return (T)_serviceCache.GetOrAdd(cacheKey, _ =>
            {
                // Construct the service dynamically
                if (typeof(T) == typeof(IAiCompletionService))
                {
                    return CreateAiCompletionService(documentProcessName);
                }
                if (typeof(T) == typeof(IBodyTextGenerator))
                {
                    return CreateBodyTextGenerator(documentProcessName);
                }
                if (typeof(T) == typeof(IKernelMemoryRepository))
                {
                    return CreateKernelMemoryRepository(documentProcessName);
                }

                return null;
            })!;
        }

        private IAiCompletionService? CreateAiCompletionService(string documentProcessName)
        { 
            var parametersGeneric = _serviceProvider.GetRequiredService<AiCompletionServiceParameters<GenericAiCompletionService>>();
            var parametersAgentic = _serviceProvider.GetRequiredService<AiCompletionServiceParameters<AgentAiCompletionService>>();

            // Find the right AiCompletionService for the document process
            var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
            var documentProcessDefinition = documentProcessInfoService.GetDocumentProcessInfoByShortName(documentProcessName);

            if (documentProcessDefinition == null)
            {
                throw new InvalidOperationException($"Document process '{documentProcessName}' not found.");
            }

            var aiCompletionService = documentProcessDefinition.CompletionServiceType;

            if (aiCompletionService == null)
            {
                throw new InvalidOperationException($"No AI completion service found for document process '{documentProcessName}'.");
            }

            if (aiCompletionService == DocumentProcessCompletionServiceType.GenericAiCompletionService)
            {
                return new GenericAiCompletionService(_dbContextFactory, parametersGeneric, documentProcessName);
            }

            if (aiCompletionService == DocumentProcessCompletionServiceType.AgentAiCompletionService)
            {
                return new AgentAiCompletionService(_dbContextFactory, parametersAgentic, documentProcessName);
            }

            throw new InvalidOperationException($"Unsupported AI completion service type '{aiCompletionService}' for document process '{documentProcessName}'.");

        }

        private IBodyTextGenerator? CreateBodyTextGenerator(string documentProcessName)
        {
            var searchOptionsFactory = _serviceProvider.GetRequiredService<IConsolidatedSearchOptionsFactory>();
            var logger = _serviceProvider.GetRequiredService<ILogger<KernelMemoryBodyTextGenerator>>();
            return new KernelMemoryBodyTextGenerator(searchOptionsFactory, this, logger, _serviceProvider);
        }

        private IKernelMemoryRepository? CreateKernelMemoryRepository(string documentProcessName)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<KernelMemoryRepository>>();
            var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
            var documentLibraryInfoService = _serviceProvider.GetRequiredService<IDocumentLibraryInfoService>();
            var kernelMemoryInstanceFactory = _serviceProvider.GetRequiredService<IKernelMemoryInstanceFactory>();

            return new KernelMemoryRepository(
                _serviceProvider,
                logger,
                documentProcessInfoService,
                documentLibraryInfoService,
                kernelMemoryInstanceFactory
            );
        }
    }
}
