using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Background service that manages MCP plugins.
    /// </summary>
    public class McpPluginBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<McpPluginBackgroundService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPluginBackgroundService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logger">The logger.</param>
        public McpPluginBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<McpPluginBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service.
        /// </summary>
        /// <param name="stoppingToken">The stopping token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("MCP Plugin Background Service is starting");

                // Get the MCP plugin manager from the service provider
                using var scope = _serviceProvider.CreateScope();
                var mcpPluginManager = scope.ServiceProvider.GetService<McpPluginManager>();

                if (mcpPluginManager == null)
                {
                    _logger.LogWarning("MCP Plugin Manager is not available. MCP plugins will not be loaded.");
                    return;
                }

                // Ensure MCP plugins are loaded
                await mcpPluginManager.EnsurePluginsLoadedAsync();

                // Wait for the application to stop
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the token is canceled
                _logger.LogInformation("MCP Plugin Background Service is stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in the MCP Plugin Background Service");
            }
            finally
            {
                _logger.LogInformation("MCP Plugin Background Service has stopped");
            }
        }
    }
}