// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Plugins.Default.GeographicalData.Models;

public class GetLatitudeAndLongitudeForLocationResponse
{
    /// <summary>
    /// Latitude of the location represented as a double/number
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of the location represented as a double/number
    /// </summary>
    public double Longitude { get; set; }
}