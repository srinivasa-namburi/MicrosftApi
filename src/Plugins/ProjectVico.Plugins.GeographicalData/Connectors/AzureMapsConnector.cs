using Azure;
using Azure.Core.GeoJson;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;

namespace ProjectVico.Plugins.GeographicalData.Connectors;

public interface IMappingConnector
{
    Task<List<string>> GetFacilitiesAsync(double latitude, double longitude, int radiusInMeters, int maxResults);
}

public class AzureMapsConnector : IMappingConnector
{
    private readonly MapsSearchClient _mapsSearchClient;

    public AzureMapsConnector(string apiKey)
    {
        this._mapsSearchClient = new MapsSearchClient(new AzureKeyCredential(apiKey));
    }
    
    public async Task<List<string>> GetFacilitiesAsync(double latitude, double longitude, int radiusInMeters, int maxResults)
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
}
