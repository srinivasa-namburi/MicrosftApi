using System.Globalization;
using BAMCIS.GeoJSON;
using Newtonsoft.Json;
using ProjectVico.Plugins.GeographicalData.Models;

namespace ProjectVico.Plugins.GeographicalData.Connectors;

public interface IEarthquakeConnector
{
    Task<List<EarthquakeDetail>> GetDetailedEarthquakesForMinMagnitude(double latitude, double longitude, int radius, int maxResults, double minMagnitude, DateOnly minDate, DateOnly maxDate, string sortOrder);
}

public class USGSEarthquakeConnector : IEarthquakeConnector
{

    public async Task<List<EarthquakeDetail>> GetDetailedEarthquakesForMinMagnitude(double latitude, double longitude, int radius, int maxResults,
        double minMagnitude, DateOnly minDate, DateOnly maxDate, string sortOrder = "time")
    {
        List<EarthquakeDetail> searchResult =
            await this.CallUSGSEarthquakeAPI(latitude, longitude, radius, maxResults, minMagnitude, minDate, maxDate, sortOrder);

        return searchResult;
    }

    private async Task<List<EarthquakeDetail>> CallUSGSEarthquakeAPI(double latitude, double longitude, int radius, int maxResults, double minMagnitude, DateOnly minDate, DateOnly maxDate, string sortOrder)
    {
        //todo: return list of json objects
        Console.WriteLine("Calling USGS earthquake API at earthquake.usgs.gov");

        // Validate that radius is no more than 150km. If it is, set it to 150km (the max allowed by the API).
        if (radius > 20000)
        {
            radius = 20000;
            Console.WriteLine("Set max radius to 20.000 KM, as the USGS Earthquake API doesn't support a larger radius");
        }

        // Convert all numeric values to strings - make sure to use invariant culture and replace decimal separator with dot if it's a comma
        var latitudeString = latitude.ToString(CultureInfo.InvariantCulture);
        var longitudeString = longitude.ToString(CultureInfo.InvariantCulture);
        var radiusString = radius.ToString(CultureInfo.InvariantCulture);
        var minMagnitudeString = minMagnitude.ToString(CultureInfo.InvariantCulture);
        var maxResultsString = maxResults.ToString(CultureInfo.InvariantCulture);

        // For latitude, longitude and minMagnitude, replace comma with dot if it's a comma
        latitudeString = latitudeString.Replace(",", ".");
        longitudeString = longitudeString.Replace(",", ".");
        minMagnitudeString = minMagnitudeString.Replace(",", ".");


        // Set Date Strings to the format required by the USGS Earthquake API - YYYY-MM-DD
        var minDateString = minDate.ToString("yyyy-MM-dd");
        var maxDateString = maxDate.ToString("yyyy-MM-dd");


        using (var client = new HttpClient())
        {
            var apiUrl = "https://earthquake.usgs.gov/fdsnws/event/1/query";
            var parameters =
                $"format=geojson&latitude={latitudeString}&longitude={longitudeString}&maxradiuskm={radiusString}&minmagnitude={minMagnitudeString}&limit={maxResultsString}&starttime={minDateString}&endtime={maxDateString}&orderby={sortOrder}";
            //TODO: ADD MIN AND MAX DATE (5 YEARS BACK), SORT BY MAGNITUDE

            var response = await client.GetAsync($"{apiUrl}?{parameters}");

            var earthquakeDetails = new List<EarthquakeDetail>();
            if (response.IsSuccessStatusCode)
            {
                // The result is returned as GeoJSON (https://datatracker.ietf.org/doc/html/rfc7946).
                // Convert the result to a List of ProjectVico.Plugins.GeographicalData.Models.EarthQuakeDetail objects.

                // Read the response content as a string.
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.Write("responsecontent:", responseContent);
                Console.WriteLine();

                // Convert the response content to a GeoJSON FeatureCollection
                var featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(responseContent);

                // Create a list of ProjectVico.Plugins.GeographicalData.Models.EarthQuakeDetail objects from the FeatureCollection
                foreach (var feature in featureCollection.Features)
                {
                    try
                    {
                        // Some Data is not formatted properly - we just have to ignore that record

                        var earthquakeDetail = new EarthquakeDetail()
                        {
                            //TimeUtc = feature.Properties["time"].ToString(),
                            Place = feature.Properties["place"]?.ToString(),
                            Magnitude = Convert.ToSingle(feature.Properties["mag"]?.ToString(),
                                CultureInfo.InvariantCulture),
                            //Depth = Convert.ToSingle(feature.Properties["depth"].ToString(), CultureInfo.InvariantCulture),
                            Tsunami = feature.Properties["tsunami"]?.ToString() == "1",
                            Website = feature.Properties["url"]?.ToString()
                        };

                        // Convert from UNIX UTC time to DateTime and set the TimeUtc variable in the earthQuakeDetail
                        var unixTime = Convert.ToDouble(feature.Properties["time"]?.ToString(),
                            CultureInfo.InvariantCulture);
                        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)unixTime).UtcDateTime;
                        earthquakeDetail.TimeUtc = dateTime;


                        // Get the Latitude and Longitude from the GeoJSON Point object.
                        var point = (Point)feature.Geometry;
                        earthquakeDetail.Latitude = point.Coordinates.Latitude;
                        earthquakeDetail.Longitude = point.Coordinates.Longitude;

                        earthquakeDetails.Add(earthquakeDetail);
                    }
                    catch
                    {
                        Console.WriteLine("Recieved a bad record from the USGS API - Ignoring. For reference, here is the record:");
                        Console.WriteLine(feature.ToString());

                    }
                }

                Console.WriteLine($"Found {earthquakeDetails.Count} earthquakes for location with latitude {latitudeString}, longitude {longitudeString}- returning details");

                return earthquakeDetails;

            }
            else
            {
                // Handle the error or return an empty list.
                return new List<EarthquakeDetail>();
            }
        }
    }





}
