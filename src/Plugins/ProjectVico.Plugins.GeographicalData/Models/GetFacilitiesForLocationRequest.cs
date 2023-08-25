// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace ProjectVico.Plugins.GeographicalData.Models;

/// <summary>
/// Request containing latitude and longitude for a location.
/// </summary>
public class GetFacilitiesForLocationRequest
{
    /// <summary>
    /// Latitude for the location to search for facilities. This is a decimal number. Typically the first in a list of 2 numbers.
    /// </summary>
    [OpenApiProperty(Description = "Latitude of the location to search for facilities")]
    public string Latitude { get; set; }
    /// <summary>
    /// Longitude for the location to search for facilities. This is a decimal number. Typically the second and last in a list of 2 numbers.
    /// </summary>
    [OpenApiProperty(Description = "Longitude of the location to search for facilities")]
    public string Longitude { get; set; }

    /// <summary>
    /// Radius in meters to search for facilities. Must be an integer. If fractional, please round to nearest integer.
    /// </summary>
    [OpenApiProperty(Description = "The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.")]
    public string RadiusInMeters { get; set; }

    /// <summary>
    /// Max number of results to return. Cannot be higher than 100.
    ///</summary>
    [OpenApiProperty(Description = "The max number of results to return. Cannot be more than 100, must be an integer.")]
    public string MaxResults { get; set; }
}
