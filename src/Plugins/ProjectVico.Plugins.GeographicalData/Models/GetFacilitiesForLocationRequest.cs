// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Plugins.GeographicalData.Models;

/// <summary>
/// Request containing latitude and longitude for a location.
/// </summary>
public class GetFacilitiesForLocationRequest
{
    /// <summary>
    /// Latitude for the location to search for facilities. This is a decimal number. Typically the first in a list of 2 numbers.
    /// </summary>
    public string Latitude { get; set; }
    /// <summary>
    /// Longitude for the location to search for facilities. This is a decimal number. Typically the second and last in a list of 2 numbers.
    /// </summary>
    public string Longitude { get; set; }

    /// <summary>
    /// Radius in meters to search for facilities. Must be an integer. If fractional, please round to nearest integer.
    /// </summary>
    public string RadiusInMeters { get; set; }

    /// <summary>
    /// Max number of results to return. Cannot be higher than 100.
    ///</summary>
    public string MaxResults { get; set; }
}
