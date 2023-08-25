// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace ProjectVico.Plugins.GeographicalData.Models;

public class GetLatitudeAndLongitudeForLocationResponse
{
    /// <summary>
    /// The latitude part of the coordinates
    /// </summary>
    [OpenApiProperty(Description = "Latitude of the location")]
    public string Latitude { get; set; }
    /// <summary>
    /// The longitude part of the coordinates
    /// </summary>
    
    [OpenApiProperty(Description = "Longitude of the location")]
    public string Longitude { get; set; }

}
