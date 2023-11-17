// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace ProjectVico.Plugins.GeographicalData.Models;

/// <summary>
/// Request containing latitude and longitude for a location.
/// </summary>
public class GetEarthquakesForLocationRequest
{
    /// <summary>
    /// Latitude for the location to search for earthquakes. This is a decimal number. Typically the first in a list of 2 numbers.
    /// </summary>
    [OpenApiProperty(Description = "Latitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees")]
    public string Latitude { get; set; }
    /// <summary>
    /// Longitude for the location to search for earthquakes. This is a decimal number. Typically the second and last in a list of 2 numbers.
    /// </summary>
    [OpenApiProperty(Description = "Longitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees.")]
    public string Longitude { get; set; }

    /// <summary>
    /// Radius in meters to search for earthquakes. Must be an integer. If fractional, please round to nearest integer.
    /// </summary>
    [OpenApiProperty(Description = "The radius/area in kilometers to search for earthquakes from the supplied geographical coordinate (latitude and longitude). Decimal from 0 to 20001.6km.")]
    public double MaxRadiusKm { get; set; }

    /// <summary>
    /// Radius in meters to search for earthquakes. Must be an integer. If fractional, please round to nearest integer.
    /// </summary>
    [OpenApiProperty(Description = "The minimum magnitude of earthquakes to include from the supplied geograhical coordinate (latitude and longitude). Decimal.")]
    public double MinMagnitude { get; set; }

    /// <summary>
    /// Max number of results to return. Cannot be higher than 100.
    ///</summary>
    [OpenApiProperty(Description = "The max number of results to return. Cannot be more than 100, must be an integer.")]
    public int MaxResults { get; set; }
}
