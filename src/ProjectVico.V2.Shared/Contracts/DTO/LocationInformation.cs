namespace ProjectVico.V2.Shared.Contracts.DTO;

public class LocationInformation
{
    public LocationInformation()
    {
        
    }
    public LocationInformation(double Latitude, double Longitude)
    {
        this.Latitude = Latitude;
        this.Longitude = Longitude;
    }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
}