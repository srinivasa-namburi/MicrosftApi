using Azure;
using Azure.Core.GeoJson;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;

namespace LicenseCopilot.Framework.Connectors;

public interface IMappingConnector
{
    Task<List<string>> GetFacilitiesAsync(double latitude, double longitude, int radiusInMetres, int maxResults);
}

public class AzureMapsConnector : IMappingConnector
{
    private readonly MapsSearchClient _mapsSearchClient;

    public AzureMapsConnector(string apiKey)
    {
        _mapsSearchClient = new MapsSearchClient(new AzureKeyCredential(apiKey));
    }
    
    public async Task<List<string>> GetFacilitiesAsync(double latitude, double longitude, int radiusInMetres, int maxResults)
    {
        var resultList = new List<string>();

        var poiCategories = await _mapsSearchClient.GetPointOfInterestCategoryTreeAsync(SearchLanguage.EnglishUsa);

        var schoolCategories = poiCategories.Value.Categories.Where(x => x.Name.Contains("School"));
        var schoolCategoryIds = schoolCategories.Select(x => x.Id).AsEnumerable().Cast<int>().Take(10);

        // Call Azure Maps API

        SearchAddressResult searchResult = await _mapsSearchClient.SearchNearbyPointOfInterestAsync(new SearchNearbyPointOfInterestOptions()
        {
            Coordinates = new GeoPosition(longitude, latitude),
            Language = SearchLanguage.EnglishUsa,
            RadiusInMeters = radiusInMetres,
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