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
    public VectorStoreWarmupHostedService(ILogger<VectorStoreWarmupHostedService> logger, IOptionsMonitor<ServiceConfigurationOptions> rootOptionsMonitor, IEnumerable<ISemanticKernelVectorStoreProvider> providers)
    { _logger = logger; _options = rootOptionsMonitor.CurrentValue.GreenlightServices.VectorStore; _providers = providers; rootOptionsMonitor.OnChange(o => _options = o.GreenlightServices.VectorStore); }
    public async Task StartAsync(CancellationToken cancellationToken)
    { if (!_options.EnableWarmup) return; try { foreach (var p in _providers) { await p.EnsureCollectionAsync("warmup"); } _logger.LogInformation("Vector store warmup completed"); } catch (Exception ex) { _logger.LogWarning(ex, "Vector store warmup errors (continuing)"); } }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
