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
    public double Latitude { get; set; }
    /// <summary>
    /// Longitude for the location to search for facilities. This is a decimal number. Typically the second and last in a list of 2 numbers.
    /// </summary>
    public double Longitude { get; set; }
}
