// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Plugins.Default.GeographicalData.Models;

/// <summary>
///     Request containing latitude and longitude for a location.
/// </summary>
public class GetFacilitiesForLocationRequest
{
    /// <summary>
    /// Latitude of the location to search for facilities represented as a string
    /// </summary>
    public string Latitude { get; set; }

    /// <summary>
    /// Longitude of the location to search for facilities represented as a string
    /// </summary>
    public string Longitude { get; set; }

    /// <summary>
    /// The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude).
    /// Must be an integer, no fractional numbers.
    /// </summary>
    public int RadiusInMeters { get; set; }

    /// <summary>
    /// The max number of results to return. Cannot be more than 100, must be an integer.
    /// </summary>
    public int MaxResults { get; set; }
}