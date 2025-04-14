using System.ComponentModel;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Connectors;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Models;

namespace Microsoft.Greenlight.Plugins.Default.EarthQuake;

public class EarthquakePlugin : IPluginImplementation
{
    private readonly IEarthquakeConnector _earthquakeConnector;

    public EarthquakePlugin(IEarthquakeConnector earthquakeConnector)
    {
        _earthquakeConnector = earthquakeConnector;
    }

    [KernelFunction("GetEarthquakesByLatitudeAndLongitude")]
    [Description("Gets a list of earthquakes from a location based on location's coordinates (latitude and longitude). If no minMagnitude is supplied, it is set to '3.5' by default.")]
    public async Task<List<EarthquakeDetail>> GetEarthquakesByLatitudeAndLongitude(
        [Description("Latitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees")]
        double latitude,
        [Description("Longitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees.")]
        double longitude,
        [Description("The beginning of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD")]
        string minDateString,
        [Description("The end of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD")]
        string maxDateString,
        [Description("The sort order of the returned results. Can be 'time' for descending time, 'time-asc' for ascending time, 'magnitude' for descending magnitude or 'magnitude-asc' for ascending magnitude. By default or if not specified, uses 'time' for the most recent first and sorted by time in a descending order.")]
        string sortOrder = "time",
        [Description("The minimum magnitude of earthquakes to include from the supplied geograhical coordinate (latitude and longitude). Decimal.")]
        double minMagnitude = 3.5,
        [Description("The max number of results to return. Cannot be more than 100, must be an integer.")]
        int maxResults = 10,
        [Description("The radius/area in kilometers to search for earthquakes from the supplied geographical coordinate (latitude and longitude). Integer from 0 to 20001.6km.")]
        int maxRadiusKm = 20
    )
    {
        if (!string.IsNullOrEmpty(sortOrder))
        {
            // Sort Order must be one of time, time-asc, magnitude or magnitude-asc.
            if (sortOrder != "time" && sortOrder != "time-asc" && sortOrder != "magnitude" && sortOrder != "magnitude-asc")
            {
                throw new ArgumentException("Sort order must be one of time, time-asc, magnitude or magnitude-asc");
            }
        }

        DateOnly minDate;
        if (!string.IsNullOrEmpty(minDateString))
        {
            if (!DateOnly.TryParse(minDateString, out minDate))
            {
                throw new ArgumentException("Mindate must be a date in the format YYYY-MM-DD");
            }
        }
        else
        {
            // If no minimum date is specified, search the last 5 years
            minDate = DateOnly.FromDateTime(DateTime.Now.AddYears(-5));
        }

        DateOnly maxDate;
        if (!string.IsNullOrEmpty(maxDateString))
        {
            if (!DateOnly.TryParse(maxDateString, out maxDate))
            {
                throw new ArgumentException("Maxdate must be a date in the format YYYY-MM-DD");
            }
        }
        else
        {
            // If no maximum date is specified, set it to today
            maxDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var responseData = await GetDetailedEarthquakesForLatLongAsync(latitude, longitude, maxRadiusKm, maxResults, minMagnitude, minDate, maxDate, sortOrder);
        return responseData;
    }

    private async Task<List<EarthquakeDetail>> GetDetailedEarthquakesForLatLongAsync(double latitude, double longitude,
        int radius, int maxResults, double minMagnitude, DateOnly minDate, DateOnly maxDate, string sortOrder = "time")
    {
        var earthquakes = await _earthquakeConnector.GetDetailedEarthquakesForMinMagnitude(
            latitude,
            longitude,
            radius,
            maxResults,
            minMagnitude,
            minDate,
            maxDate,
            sortOrder);

        return earthquakes;
    }
}
