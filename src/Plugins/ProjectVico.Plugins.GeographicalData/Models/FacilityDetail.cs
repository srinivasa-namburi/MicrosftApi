// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace ProjectVico.Plugins.GeographicalData.Models;

public class FacilityDetail
{
    [OpenApiProperty(Description = "Name of the facility")]
    public string? Name { get; set; }
    [OpenApiProperty(Description = "Address of the facility")]
    public string? Address { get; set; }
    [OpenApiProperty(Description = "City of the facility")]
    public string? City { get; set; }
    [OpenApiProperty(Description = "State, region, or province of the facility")]
    public string? State { get; set; }
    [OpenApiProperty(Description = "Postalcode of the facility")]
    public string? PostalCode { get; set; }
    [OpenApiProperty(Description = "Country of the facility")]
    public string? Country { get; set; }
    [OpenApiProperty(Description = "Phone number of the facility")]
    public string? PhoneNumber { get; set; }
    [OpenApiProperty(Description = "Website of the facility")]
    public string? Website { get; set; }
    [OpenApiProperty(Description = "Categories the facility belongs to represented as strings")]
    public List<string> SearchProviderCategories { get; set; } = null!;
    [OpenApiProperty(Description = "Distance in meters from the search point of this facility")]
    public double? DistanceInMetersFromSearchPoint { get; set; }
    [OpenApiProperty(Description = "Latitude of the facility")]
    public double Latitude { get; set; }
    [OpenApiProperty(Description = "Longitude of the facility")]
    public double Longitude { get; set; }

}
