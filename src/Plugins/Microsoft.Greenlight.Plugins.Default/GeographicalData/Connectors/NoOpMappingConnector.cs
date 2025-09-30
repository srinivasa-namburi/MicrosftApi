// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Models;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;

/// <summary>
/// No-op implementation of <see cref="IMappingConnector"/> used when Azure Maps is not configured.
/// Throws informative exceptions if invoked to signal missing configuration.
/// </summary>
public sealed class NoOpMappingConnector : IMappingConnector
{
    private readonly ILogger<NoOpMappingConnector> _logger;
    private const string MissingConfigMessage =
        "Azure Maps is not configured. Set 'ServiceConfiguration:AzureMaps:Key' to enable GeographicalData features (also available under Administration->Configuration->Serets";

    /// <summary>
    /// Initializes a new instance of the <see cref="NoOpMappingConnector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public NoOpMappingConnector(ILogger<NoOpMappingConnector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<string>> GetSchools(double latitude, double longitude, int radiusInMeters, int maxResults)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }

    /// <inheritdoc />
    public Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocation(string location)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }

    /// <inheritdoc />
    public Task<List<int>> GetCategoryIdsForCategoryName(string categoryName)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }

    /// <inheritdoc />
    public Task<List<string>> GetFacilitiesForCategoryName(double latitude, double longitude, int radiusInMeters, int maxResults, string categoryName)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }

    /// <inheritdoc />
    public Task<List<FacilityDetail>> GetDetailedFacilitiesForCategoryName(double latitude, double longitude, int radiusInMeters, int maxResults, string categoryName)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }

    /// <inheritdoc />
    public Stream GetMapImageStream(double longitude, double latitude, int zoom, int widthInPixels = 768, int heightInPixels = 512)
    {
        _logger.LogWarning("{Message}", MissingConfigMessage);
        throw new InvalidOperationException(MissingConfigMessage);
    }
}
