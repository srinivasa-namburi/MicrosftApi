// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search.Internal;

internal sealed class VectorStoreWarmupHostedService : IHostedService
{
    private readonly ILogger<VectorStoreWarmupHostedService> _logger;
    private VectorStoreOptions _options;
    private readonly IEnumerable<ISemanticKernelVectorStoreProvider> _providers;
    private readonly IAiEmbeddingService _embeddingService;

    public VectorStoreWarmupHostedService(
        ILogger<VectorStoreWarmupHostedService> logger,
        IOptionsMonitor<ServiceConfigurationOptions> rootOptionsMonitor,
        IEnumerable<ISemanticKernelVectorStoreProvider> providers,
        IAiEmbeddingService embeddingService)
    {
        _logger = logger;
        _options = rootOptionsMonitor.CurrentValue.GreenlightServices.VectorStore;
        _providers = providers;
        _embeddingService = embeddingService;
        rootOptionsMonitor.OnChange(o => _options = o.GreenlightServices.VectorStore);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableWarmup)
        {
            return;
        }

        try
        {
            // Resolve embedding dimensionality using the globally configured embedding deployment
            int dims;
            try
            {
                var probe = await _embeddingService.GenerateEmbeddingsAsync("warmup");
                dims = probe?.Length ?? (_options.VectorSize > 0 ? _options.VectorSize : 1536);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to probe embedding dimensions during warmup; falling back to configured/default dimensions");
                dims = _options.VectorSize > 0 ? _options.VectorSize : 1536;
            }

            // To avoid dimensionality mismatch when defaults change across restarts,
            // clear the warmup collection before ensuring it with the current dimensions.
            foreach (var p in _providers)
            {
                try
                {
                    await p.ClearCollectionAsync("warmup", cancellationToken);
                }
                catch (Exception exClear)
                {
                    // Continue even if clear fails; EnsureCollectionAsync below may still succeed.
                    _logger.LogDebug(exClear, "Warmup clear failed (continuing)");
                }

                await p.EnsureCollectionAsync("warmup", dims, cancellationToken);
            }

            _logger.LogInformation("Vector store warmup completed with dimensions {Dims}", dims);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector store warmup errors (continuing)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
