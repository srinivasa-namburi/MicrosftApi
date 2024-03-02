// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Plugins.Earthquake.Models;

/// <summary>
///     Request containing latitude and longitude for a location.
/// </summary>
public class GetEarthquakesForLocationRequest
{
    /// <summary>
    /// Latitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees
    /// </summary>
    public string Latitude { get; set; }

    /// <summary>
    /// Longitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees.
    /// </summary>
    public string Longitude { get; set; }

    /// <summary>
    /// The radius/area in kilometers to search for earthquakes from the supplied geographical coordinate (latitude and longitude). Decimal from 0 to 20001.6km.
    /// </summary>
    public double MaxRadiusKm { get; set; }

    /// <summary>
    /// The minimum magnitude of earthquakes to include from the supplied geograhical coordinate (latitude and longitude). Decimal.
    /// </summary>
    public double MinMagnitude { get; set; }

    /// <summary>
    /// The max number of results to return. Cannot be more than 100, must be an integer.
    /// </summary>
    public int MaxResults { get; set; }
}