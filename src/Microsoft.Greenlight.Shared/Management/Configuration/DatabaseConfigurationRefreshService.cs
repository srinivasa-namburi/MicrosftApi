using Humanizer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Management.Configuration;

/// <summary>
/// A hosted service that ensures database configuration is loaded after application startup
/// and periodically refreshes it.
/// </summary>
public class DatabaseConfigurationRefreshService : IHostedService, IDisposable
{
    private readonly EfCoreConfigurationProvider _configProvider;
    private readonly ILogger<DatabaseConfigurationRefreshService> _logger;
    private Timer? _configRefreshTimer;
    private TimeSpan _refreshInterval = 10.Minutes();

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseConfigurationRefreshService"/> class.
    /// </summary>
    /// <param name="configProvider">The configuration provider.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseConfigurationRefreshService(
        EfCoreConfigurationProvider configProvider,
        ILogger<DatabaseConfigurationRefreshService> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the service, ensuring database configuration is properly initialized
    /// and setting up a recurring timer to refresh it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DatabaseConfigurationInitializerService");
        
        try
        {
            _logger.LogDebug("Loading initial configuration from database");
            
            // Initial load of the data
            _configProvider.Load();
            _logger.LogInformation("Database configuration provider initialized successfully");

            // Randomize the refresh interval within 10 seconds to avoid all instances refreshing at the same time
            var randomOffset = TimeSpan.FromSeconds(new Random().Next(0, 10));
            _refreshInterval = _refreshInterval.Add(randomOffset);
            
            // Set up the periodic refresh timer
            _configRefreshTimer = new Timer(
                RefreshConfiguration, 
                null, 
                _refreshInterval, // Initial delay before first refresh
                _refreshInterval); // Interval between refreshes
            
            _logger.LogInformation("Configuration refresh timer started with interval of {RefreshInterval} seconds", 
                _refreshInterval.Seconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database configuration provider");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Refreshes the configuration by reloading it from the database.
    /// </summary>
    /// <param name="state">Not used.</param>
    private void RefreshConfiguration(object? state)
    {
        try
        {
            _configProvider.Load();
            _logger.LogDebug("Configuration refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing configuration from database");
        }
    }

    /// <summary>
    /// Stops the service, cleaning up resources.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Stopping {nameof(DatabaseConfigurationRefreshService)}");
        
        _configRefreshTimer?.Change(Timeout.Infinite, 0);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes of resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _configRefreshTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
