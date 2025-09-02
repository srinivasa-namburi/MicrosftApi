// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Service for managing dynamic NavMenu state and refreshing when document processes change.
/// </summary>
public interface INavMenuStateService
{
    /// <summary>
    /// Event raised when the NavMenu should be refreshed.
    /// </summary>
    event Action? NavMenuChanged;

    /// <summary>
    /// Gets the current list of document processes.
    /// </summary>
    Task<List<DocumentProcessInfo>> GetDocumentProcessesAsync(bool forceRefresh = false);

    /// <summary>
    /// Gets the current feature flags.
    /// </summary>
    Task<object?> GetFeatureFlagsAsync(bool forceRefresh = false);

    /// <summary>
    /// Notifies that document processes have changed and NavMenu should refresh.
    /// </summary>
    Task NotifyDocumentProcessesChangedAsync();

    /// <summary>
    /// Refreshes all cached NavMenu data.
    /// </summary>
    Task RefreshAllAsync();
}

/// <summary>
/// Service for managing dynamic NavMenu state with caching and change notifications.
/// </summary>
public sealed class NavMenuStateService : INavMenuStateService
{
    private readonly IDocumentProcessApiClient _documentProcessApiClient;
    private readonly IConfigurationApiClient _configurationApiClient;
    private readonly ILogger<NavMenuStateService> _logger;

    // Cache with expiry
    private List<DocumentProcessInfo>? _cachedDocumentProcesses;
    private object? _cachedFeatureFlags;
    private DateTime _lastDocumentProcessRefresh = DateTime.MinValue;
    private DateTime _lastFeatureFlagsRefresh = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public event Action? NavMenuChanged;

    public NavMenuStateService(
        IDocumentProcessApiClient documentProcessApiClient,
        IConfigurationApiClient configurationApiClient,
        ILogger<NavMenuStateService> logger)
    {
        _documentProcessApiClient = documentProcessApiClient;
        _configurationApiClient = configurationApiClient;
        _logger = logger;
    }

    public async Task<List<DocumentProcessInfo>> GetDocumentProcessesAsync(bool forceRefresh = false)
    {
        if (forceRefresh || 
            _cachedDocumentProcesses == null || 
            DateTime.UtcNow - _lastDocumentProcessRefresh > _cacheExpiry)
        {
            try
            {
                _cachedDocumentProcesses = await _documentProcessApiClient.GetAllDocumentProcessInfoAsync();
                _lastDocumentProcessRefresh = DateTime.UtcNow;
                _logger.LogDebug("Refreshed document processes cache, found {Count} processes", _cachedDocumentProcesses.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh document processes");
                // Return cached data if available, otherwise empty list
                return _cachedDocumentProcesses ?? new List<DocumentProcessInfo>();
            }
        }

        return _cachedDocumentProcesses ?? new List<DocumentProcessInfo>();
    }

    public async Task<object?> GetFeatureFlagsAsync(bool forceRefresh = false)
    {
        if (forceRefresh || 
            _cachedFeatureFlags == null || 
            DateTime.UtcNow - _lastFeatureFlagsRefresh > _cacheExpiry)
        {
            try
            {
                _cachedFeatureFlags = await _configurationApiClient.GetFeatureFlagsAsync();
                _lastFeatureFlagsRefresh = DateTime.UtcNow;
                _logger.LogDebug("Refreshed feature flags cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh feature flags");
                // Return cached data if available
                return _cachedFeatureFlags;
            }
        }

        return _cachedFeatureFlags;
    }

    public async Task NotifyDocumentProcessesChangedAsync()
    {
        _logger.LogDebug("Document processes changed, invalidating cache and notifying subscribers");
        
        // Invalidate cache
        _cachedDocumentProcesses = null;
        _lastDocumentProcessRefresh = DateTime.MinValue;

        // Pre-fetch new data
        await GetDocumentProcessesAsync(forceRefresh: true);

        // Notify subscribers
        NavMenuChanged?.Invoke();
    }

    public async Task RefreshAllAsync()
    {
        _logger.LogDebug("Refreshing all NavMenu data");
        
        await Task.WhenAll(
            GetDocumentProcessesAsync(forceRefresh: true),
            GetFeatureFlagsAsync(forceRefresh: true)
        );

        NavMenuChanged?.Invoke();
    }
}