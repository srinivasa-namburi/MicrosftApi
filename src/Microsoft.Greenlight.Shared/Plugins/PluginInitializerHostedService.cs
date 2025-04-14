using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Extensions.Plugins;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Hosted service that initializes all plugins, both dynamic and static.
    /// Provides retry capability for failed plugin initializations.
    /// </summary>
    public class PluginInitializerHostedService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginInitializerHostedService> _logger;
        private readonly ConcurrentDictionary<Type, DateTime> _failedPlugins = new();
        private Timer? _retryTimer;
        private const int RetryIntervalSeconds = 30;
        private bool _initialAttemptCompleted = false;
        
        /// <summary>
        /// The PluginInitializerHostedService constructor - takes in the service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logger">The logger instance.</param>
        public PluginInitializerHostedService(IServiceProvider serviceProvider, ILogger<PluginInitializerHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                _logger.LogInformation("Starting plugin initialization");
                var initializers = scope.ServiceProvider.GetServices<IPluginInitializer>().ToList();
                
                // Perform initial initialization attempt
                await InitializePluginsAsync(scope.ServiceProvider,initializers, cancellationToken);
                _initialAttemptCompleted = true;
                
                // If we have any failed plugins, set up the retry timer
                if (_failedPlugins.Count > 0)
                {
                    _logger.LogWarning("{FailedCount} plugins failed to initialize. Will retry every {RetryInterval} seconds.", 
                        _failedPlugins.Count, RetryIntervalSeconds);
                    
                    _retryTimer = new Timer(
                        RetryFailedPlugins, 
                        null, 
                        TimeSpan.FromSeconds(RetryIntervalSeconds), 
                        TimeSpan.FromSeconds(RetryIntervalSeconds));
                }
                else
                {
                    _logger.LogInformation("All plugins initialized successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial plugin initialization process");
                // We don't rethrow to prevent application crash on startup
            }
        }

        private async Task InitializePluginsAsync(IServiceProvider scopeServiceProvider,
            IEnumerable<IPluginInitializer> initializers, CancellationToken cancellationToken)
        {
            foreach (var initializer in initializers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Skip if we've already processed this initializer successfully
                var initializerType = initializer.GetType();
                if (_initialAttemptCompleted && !_failedPlugins.ContainsKey(initializerType))
                {
                    continue;
                }

                try
                {
                    _logger.LogInformation("Initializing plugin: {PluginType}", initializerType.FullName);
                    await initializer.InitializeAsync(scopeServiceProvider);
                    
                    // If this was a retry and it succeeded, remove from failed list
                    if (_failedPlugins.TryRemove(initializerType, out _))
                    {
                        _logger.LogInformation("Successfully initialized previously failed plugin: {PluginType}", 
                            initializerType.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing plugin: {PluginType}. Plugin will be retried later.", 
                        initializerType.FullName);
                    
                    // Add to failed plugins list for retry
                    _failedPlugins.TryAdd(initializerType, DateTime.UtcNow);
                }
            }
        }

        private async void RetryFailedPlugins(object? state)
        {
            try
            {
                if (_failedPlugins.Count == 0)
                {
                    _logger.LogInformation("All plugins initialized successfully. Stopping retry timer.");
                    _retryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }

                _logger.LogInformation("Retrying initialization of {FailedCount} failed plugins", _failedPlugins.Count);

                using var scope = _serviceProvider.CreateScope();
                
                // Get the initializers for the failed plugin types
                var initializers = scope.ServiceProvider.GetServices<IPluginInitializer>()
                    .Where(i => _failedPlugins.ContainsKey(i.GetType()))
                    .ToList();
                
                await InitializePluginsAsync(scope.ServiceProvider, initializers, CancellationToken.None);
                
                // Log status after retry
                if (_failedPlugins.Count > 0)
                {
                    _logger.LogWarning("{RemainingCount} plugins still failed to initialize. Will retry in {RetryInterval} seconds.", 
                        _failedPlugins.Count, RetryIntervalSeconds);
                }
                else
                {
                    _logger.LogInformation("All plugins successfully initialized after retries!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during plugin retry process");
            }
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _retryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the retry timer when the service is disposed.
        /// </summary>
        public void Dispose()
        {
            _retryTimer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
