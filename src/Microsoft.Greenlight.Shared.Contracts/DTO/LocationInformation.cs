namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the geographical location information with latitude and longitude.
/// </summary>
public class LocationInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocationInformation"/> class.
    /// </summary>
    public LocationInformation()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationInformation"/> class with specified latitude and longitude.
    /// </summary>
    /// <param name="Latitude">The latitude of the location.</param>
    /// <param name="Longitude">The longitude of the location.</param>
    public LocationInformation(double Latitude, double Longitude)
    {
        this.Latitude = Latitude;
        this.Longitude = Longitude;
    }

    /// <summary>
    /// Latitude of the location.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of the location.
    /// </summary>
    public double Longitude { get; set; }
}
