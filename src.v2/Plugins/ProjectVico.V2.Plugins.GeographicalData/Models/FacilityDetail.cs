// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Plugins.GeographicalData.Models;

public class FacilityDetail
{
    /// <summary>
    /// Name of the facility
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Address of the facility
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City of the facility
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State, region, or province of the facility
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Postal code/Zip code of the facility
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country of the facility
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Phone number of the facility
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Website of the facility
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Categories the facility belongs to represented as strings
    /// </summary>
    public List<string> SearchProviderCategories { get; set; } = null!;

    /// <summary>
    /// Distance in meters from the search point/location of this facility
    /// </summary>
    public double? DistanceInMetersFromSearchPoint { get; set; }

    /// <summary>
    /// Latitude of the facility
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of the facility
    /// </summary>
    public double Longitude { get; set; }
}