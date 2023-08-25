// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Plugins.GeographicalData.Models;

public class GetFacilitiesForLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusInMeters { get; set; }
    public int MaxResults { get; set; }
}
