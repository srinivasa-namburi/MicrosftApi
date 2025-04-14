using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Notifiers;

namespace Microsoft.Greenlight.Shared.Management
{
    /// <summary>
    /// Background service that subscribes to Orleans streams and relays messages to SignalR,
    /// replacing the original MassTransit consumers while preserving their behavior.
    /// </summary>
    public class OrleansStreamSubscriberService : BackgroundService
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<OrleansStreamSubscriberService> _logger;
        private readonly IEnumerable<IWorkStreamNotifier> _workStreamNotifiers;
        private Timer? _reconnectionTimer;
        private bool _isSubscribed;

        // Track the subscription handles by notifier
        private readonly Dictionary<string, List<object>> _subscriptionHandlesByNotifier = new();

        /// <summary>
        /// Constructs the <see cref="OrleansStreamSubscriberService"/>
        /// </summary>
        /// <param name="clusterClient">The Orleans cluster client</param>
        /// <param name="workStreamNotifiers">The work stream notifiers from DI</param>
        /// <param name="logger">Logger instance</param>
        public OrleansStreamSubscriberService(
            IClusterClient clusterClient,
            IEnumerable<IWorkStreamNotifier> workStreamNotifiers,
            ILogger<OrleansStreamSubscriberService> logger)
        {
            _clusterClient = clusterClient;
            _workStreamNotifiers = workStreamNotifiers;
            _logger = logger;
        
            _logger.LogInformation("OrleansStreamSubscriberService initialized with {Count} notifiers: {Notifiers}", 
                _workStreamNotifiers.Count(), string.Join(", ", _workStreamNotifiers.Select(n => n.Name)));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Orleans Stream Subscriber Service is starting");

            try
            {
                await InitializeSubscriptions(stoppingToken);
                
                // Set up a reconnection timer that periodically checks if subscriptions are active
                _reconnectionTimer = new Timer(async _ => await CheckAndRestoreSubscriptions(), null, 
                    TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

                // Keep the service running but check periodically for connection issues
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Orleans Stream Subscriber Service running");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Orleans Stream Subscriber Service");
            }
            finally
            {
                // Clean up subscriptions on shutdown
                _reconnectionTimer?.Dispose();
                await CleanupAllStreamSubscriptionsAsync();
                _logger.LogInformation("Orleans Stream Subscriber Service is stopping");
            }
        }

        private async Task InitializeSubscriptions(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing Orleans stream subscriptions");
            try
            {
                // Clear any existing subscriptions before re-initializing
                await CleanupAllStreamSubscriptionsAsync();
                
                // Set up stream subscriptions
                await SetupGlobalStreamSubscriptionsAsync();
                
                _isSubscribed = true;
                _logger.LogInformation("Successfully initialized all Orleans stream subscriptions");
            }
            catch (Exception ex)
            {
                _isSubscribed = false;
                _logger.LogError(ex, "Failed to initialize Orleans stream subscriptions. Will retry.");
                
                // If this happens during startup, wait and retry once
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Retrying subscription initialization after delay");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    await SetupGlobalStreamSubscriptionsAsync();
                    _isSubscribed = true;
                }
            }
        }

        private async Task CheckAndRestoreSubscriptions()
        {
            try
            {
                bool isClientConnected = false;
        
                try
                {
                    // Attempt to ping the cluster to verify connectivity
                    await _clusterClient.GetGrain<IManagementGrain>(0).GetHosts();
                    isClientConnected = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Orleans client appears to be disconnected, will attempt to resubscribe");
                    isClientConnected = false;
                }

                // Check if client is not connected or subscriptions are missing
                if (!isClientConnected || !_isSubscribed)
                {
                    _logger.LogWarning("Stream subscriptions appear to be lost or client was reconnected, attempting to resubscribe");
                    await InitializeSubscriptions(CancellationToken.None);
                }
                else if (_isSubscribed)
                {
                    // Perform a health check on subscriptions by checking handles count
                    bool hasSubscriptions = _subscriptionHandlesByNotifier.Values.Any(handles => handles.Count > 0);
                    if (!hasSubscriptions)
                    {
                        _logger.LogWarning("No subscription handles found. Attempting to resubscribe.");
                        await InitializeSubscriptions(CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogDebug("Stream subscription check completed: All subscriptions appear healthy");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking or restoring stream subscriptions");
                _isSubscribed = false;
            }
        }

        private async Task SetupGlobalStreamSubscriptionsAsync()
        {
            try
            {
                // Get the stream provider
                var streamProvider = _clusterClient.GetStreamProvider("StreamProvider");
                var notifierCount = _workStreamNotifiers.Count();
                var notifierProcessed = 0;

                // Iterate through all registered workstream notifiers and set up their subscriptions
                foreach (var notifier in _workStreamNotifiers)
                {
                    notifierProcessed++;
                    _logger.LogInformation("[{Progress}] Setting up stream subscriptions for {NotifierName} domain",
                        $"{notifierProcessed}/{notifierCount}", notifier.Name);

                    try
                    {
                        // Get all subscription handles from the notifier
                        var subscriptionHandles = await notifier.SubscribeToStreamsAsync(_clusterClient, streamProvider);

                        // Store the subscription handles for cleanup later
                        _subscriptionHandlesByNotifier[notifier.Name] = subscriptionHandles;

                        _logger.LogInformation("[{Progress}] Successfully set up {Count} stream subscriptions for {NotifierName} domain",
                            $"{notifierProcessed}/{notifierCount}", subscriptionHandles.Count, notifier.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error setting up stream subscriptions for {NotifierName}. Continuing with other notifiers", notifier.Name);
                        // Continue with other notifiers rather than failing completely
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up global stream subscriptions");
                throw;
            }
        }

        private async Task CleanupAllStreamSubscriptionsAsync()
        {
            try
            {
                // Unsubscribe from all stream subscriptions
                foreach (var notifier in _workStreamNotifiers)
                {
                    if (_subscriptionHandlesByNotifier.TryGetValue(notifier.Name, out var handles))
                    {
                        try
                        {
                            await notifier.UnsubscribeFromStreamsAsync(handles);
                            _logger.LogInformation("Unsubscribed from all streams for {NotifierName}", notifier.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error unsubscribing from streams for {NotifierName}", notifier.Name);
                            // Continue with other notifiers rather than failing completely
                        }
                    }
                }

                _subscriptionHandlesByNotifier.Clear();
                _isSubscribed = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up stream subscriptions");
            }
        }
    }
}
