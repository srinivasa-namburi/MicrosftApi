using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Dynamic
{
    /// <summary>
    /// This factory returns services for a particular document process, constructing them on demand.
    /// </summary>
    public class DynamicDocumentProcessServiceFactory : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, Task<object?>> _serviceCache = new();
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly SemaphoreSlim _creationSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the DynamicDocumentProcessServiceFactory.
        /// </summary>
        /// <param name="serviceProvider">Root service provider for constructing services.</param>
        /// <param name="dbContextFactory">Factory for DocGenerationDbContext instances.</param>
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
        public async Task<T?> GetServiceAsync<T>(string documentProcessName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            
            // Use the document process name as part of the cache key
            var cacheKey = $"{documentProcessName}-{typeof(T).Name}";

            // Retrieve or create the service asynchronously
            var serviceTask = _serviceCache.GetOrAdd(cacheKey, _ => CreateServiceAsync<T>(documentProcessName));
            
            var service = await serviceTask;
            return (T?)service;
        }

        /// <summary>
        /// Synchronous version for backward compatibility - use GetServiceAsync when possible
        /// </summary>
        /// <param name="documentProcessName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [Obsolete("Use GetServiceAsync to avoid deadlock risks. This method is kept for backward compatibility only.")]
        public T? GetService<T>(string documentProcessName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            
            // For backward compatibility, but this still has deadlock risk
            // Callers should migrate to GetServiceAsync
            try
            {
                return GetServiceAsync<T>(documentProcessName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<DynamicDocumentProcessServiceFactory>>();
                logger?.LogError(ex, "Error getting service {ServiceType} for document process {DocumentProcessName} - consider using GetServiceAsync", typeof(T).Name, documentProcessName);
                return default;
            }
        }

        private async Task<object?> CreateServiceAsync<T>(string documentProcessName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            
            await _creationSemaphore.WaitAsync();
            try
            {
                // Construct the service dynamically
                if (typeof(T) == typeof(IAiCompletionService))
                {
                    return await CreateAiCompletionServiceAsync(documentProcessName);
                }
                if (typeof(T) == typeof(IBodyTextGenerator))
                {
                    return CreateBodyTextGenerator(documentProcessName);
                }
                if (typeof(T) == typeof(IDocumentRepository))
                {
                    return await CreateDocumentRepositoryAsync(documentProcessName);
                }
                if (typeof(T) == typeof(IKernelMemoryRepository))
                {
                    return CreateKernelMemoryRepository(documentProcessName);
                }

                return null;
            }
            finally
            {
                _creationSemaphore.Release();
            }
        }

        private async Task<IAiCompletionService?> CreateAiCompletionServiceAsync(string documentProcessName)
        { 
            var parametersGeneric = _serviceProvider.GetRequiredService<AiCompletionServiceParameters<GenericAiCompletionService>>();
            var parametersAgentic = _serviceProvider.GetRequiredService<AiCompletionServiceParameters<AgentAiCompletionService>>();

            // Find the right AiCompletionService for the document process
            var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
            var documentProcessDefinition = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

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
            var logger = _serviceProvider.GetRequiredService<ILogger<RagBodyTextGenerator>>();
            return new RagBodyTextGenerator(searchOptionsFactory, this, logger, _serviceProvider);
        }

        private async Task<IDocumentRepository?> CreateDocumentRepositoryAsync(string documentProcessName)
        {
            var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
            var documentProcessDefinition = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

            if (documentProcessDefinition == null)
            {
                throw new InvalidOperationException($"Document process '{documentProcessName}' not found.");
            }

            var repositoryFactory = _serviceProvider.GetRequiredService<IDocumentRepositoryFactory>();
            // Now we can await this properly without blocking
            var repository = await repositoryFactory.CreateForDocumentProcessAsync(documentProcessDefinition);
            return repository;
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

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Protected virtual dispose method following the Dispose pattern
        /// </summary>
        /// <param name="disposing">Whether this is called from Dispose() or the finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _creationSemaphore?.Dispose();
                if (_serviceProvider is IDisposable serviceProvider)
                {
                    serviceProvider.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
