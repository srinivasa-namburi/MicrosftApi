using System.Globalization;
using Azure;
using Azure.Core.GeoJson;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using ProjectVico.Plugins.GeographicalData.Models;

namespace ProjectVico.Plugins.GeographicalData.Connectors;

public interface IMappingConnector
{
    Task<List<string>> GetFacilities(double latitude, double longitude, int radiusInMeters, int maxResults);
    Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocation(string location);
}

public class AzureMapsConnector : IMappingConnector
{
    private readonly MapsSearchClient _mapsSearchClient;

    public AzureMapsConnector(string apiKey)
    {
        this._mapsSearchClient = new MapsSearchClient(new AzureKeyCredential(apiKey));
    }
    public async Task<List<string>> GetFacilities(double latitude, double longitude, int radiusInMeters, int maxResults)
    {
        var resultList = new List<string>();

        var poiCategories = await this._mapsSearchClient.GetPointOfInterestCategoryTreeAsync(SearchLanguage.EnglishUsa);

        var schoolCategories = poiCategories.Value.Categories.Where(x => x.Name.Contains("School"));
        var schoolCategoryIds = schoolCategories.Select(x => x.Id).AsEnumerable().Cast<int>().Take(10);

        // Call Azure Maps API

        SearchAddressResult searchResult = await this._mapsSearchClient.SearchNearbyPointOfInterestAsync(new SearchNearbyPointOfInterestOptions()
        {
            Coordinates = new GeoPosition(longitude, latitude),
            Language = SearchLanguage.EnglishUsa,
            RadiusInMeters = radiusInMeters,
            //CategoryFilter = new List<int>(schoolCategoryIds.First())
            CategoryFilter = schoolCategoryIds,
            Top = maxResults
        });

        foreach (var result in searchResult.Results)
        {
            resultList.Add(result.PointOfInterest.Name);
        }
        var tempResultString = string.Join(", ", resultList);
        return resultList;
    }

    public async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocation(string location)
    {
        var locationResult= await this._mapsSearchClient.SearchAddressAsync(location, new SearchAddressOptions()
        {
            Top = 1,
            Language = SearchLanguage.EnglishUsa
        });

        var result = new GetLatitudeAndLongitudeForLocationResponse()
        {
            Latitude = locationResult.Value.Results.First().Position.Latitude.ToString(CultureInfo.InvariantCulture),
            Longitude = locationResult.Value.Results.First().Position.Longitude.ToString(CultureInfo.InvariantCulture)
        };

        return result;
    }
}
