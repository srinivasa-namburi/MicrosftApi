// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Plugins.GeographicalData.Models;

public class GetLatitudeAndLongitudeForLocationResponse
{
    /// <summary>
    /// The latitude part of the coordinates
    /// </summary>
    public double Latitude { get; set; }
    /// <summary>
    /// The longitude part of the coordinates
    /// </summary>
    public double Longitude { get; set; }

}
