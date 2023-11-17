// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace ProjectVico.Plugins.GeographicalData.Models;

public class EarthquakeDetail
{
    [OpenApiProperty(Description = "Timestamp of earthquake expressed as UTC date/time")]
    public DateTime TimeUtc { get; set; }
    [OpenApiProperty(Description = "Location of earthquake")]
    public string? Place { get; set; }
    [OpenApiProperty(Description = "Magnitude of earthquake")]
    public float? Magnitude { get; set; }
    [OpenApiProperty(Description = "Depth of earthquake")]
    public float? Depth { get; set; }
    [OpenApiProperty(Description = "Whether earthquake generated a tsunami")]
    public bool? Tsunami { get; set; }
    [OpenApiProperty(Description = "URL providing info about earthquake")]
    public string? Website { get; set; }
    [OpenApiProperty(Description = "Latitude of the earthquake epicenter")]
    public double Latitude { get; set; }
    [OpenApiProperty(Description = "Longitude of the earthquake epicenter")]
    public double Longitude { get; set; }

}
