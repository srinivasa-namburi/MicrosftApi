using System.Globalization;
using Azure;
using Azure.Core.GeoJson;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using ProjectVico.Plugins.GeographicalData.Models;

namespace ProjectVico.Plugins.GeographicalData.Connectors;

public interface IMappingConnector
{
    Task<List<string>> GetSchools(double latitude, double longitude, int radiusInMeters, int maxResults);
    Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocation(string location);
    Task<List<int>> GetCategoryIdsForCategoryName(string categoryName);

    Task<List<string>> GetFacilitiesForCategoryname(double latitude, double longitude, int radiusInMeters,
        int maxResults, string categoryName);

    Task<List<FacilityDetail>> GetDetailedFacilitiesForCategoryName(double latitude, double longitude,
        int radiusinMeters,
        int maxResults, string categoryName);
}

public class AzureMapsConnector : IMappingConnector
{
    private readonly MapsSearchClient _mapsSearchClient;

    public AzureMapsConnector(string apiKey)
    {
        this._mapsSearchClient = new MapsSearchClient(new AzureKeyCredential(apiKey));
    }

    public AzureMapsConnector(IConfiguration configuration)
    {
        this._mapsSearchClient = new MapsSearchClient(new AzureKeyCredential(configuration["AzureMapsKey"]));
    }

    public async Task<List<int>> GetCategoryIdsForCategoryName(string categoryName)
    {
        var poiCategories = await this._mapsSearchClient.GetPointOfInterestCategoryTreeAsync(SearchLanguage.EnglishUsa);

        var schoolCategories = poiCategories.Value.Categories.Where(x => x.Name.Contains(categoryName));
        var schoolCategoryIds = schoolCategories.Select(x => x.Id).AsEnumerable().Cast<int>().Take(10);

        return schoolCategoryIds.ToList();
    }

    public async Task<List<FacilityDetail>> GetDetailedFacilitiesForCategoryName(double latitude, double longitude,
        int radiusinMeters,
        int maxResults, string categoryName)
    {
        var categoryIds = await this.GetCategoryIdsForCategoryName(categoryName);
        // Call Azure Maps API
        SearchAddressResult searchResult = await this._mapsSearchClient.SearchNearbyPointOfInterestAsync(new SearchNearbyPointOfInterestOptions()
        {
            Coordinates = new GeoPosition(longitude, latitude),
            Language = SearchLanguage.EnglishUsa,
            RadiusInMeters = radiusinMeters,
            CategoryFilter = categoryIds,
            Top = maxResults
        });

        var resultList = new List<FacilityDetail>();
        foreach (var result in searchResult.Results)
        {

            var facilityDetail = new FacilityDetail()
            {
                DistanceInMetersFromSearchPoint = result.DistanceInMeters,
                Name = result.PointOfInterest.Name,
                Address = result.Address.FreeformAddress,
                City = result.Address.Municipality,
                State = result.Address.CountrySubdivision,
                PostalCode = result.Address.PostalCode,
                Country = result.Address.Country,
                PhoneNumber = result.PointOfInterest.Phone,
                //Website = result.PointOfInterest.Uri?.ToString(),
                SearchProviderCategories = result.PointOfInterest.Categories.ToList(),
                Latitude = result.Position.Latitude,
                Longitude = result.Position.Longitude
            };

            //var searchProviderCategories = result.PointOfInterest.Categories.ToList();
            //facilityDetail.SearchProviderCategories = searchProviderCategories;
            resultList.Add(facilityDetail);
        }
        return resultList;
    }

    public async Task<List<string>> GetFacilitiesForCategoryname(double latitude, double longitude, int radiusInMeters,
        int maxResults, string categoryName)
    {
        var categoryIds = await this.GetCategoryIdsForCategoryName(categoryName);
        // Call Azure Maps API

        SearchAddressResult searchResult = await this._mapsSearchClient.SearchNearbyPointOfInterestAsync(new SearchNearbyPointOfInterestOptions()
        {
            Coordinates = new GeoPosition(longitude, latitude),
            Language = SearchLanguage.EnglishUsa,
            RadiusInMeters = radiusInMeters,
            CategoryFilter = categoryIds,
            Top = maxResults
        });

        return await this.GetSearchAddressResultAsStringAsync(searchResult);
    }

    public async Task<List<string>> GetSchools(double latitude, double longitude, int radiusInMeters, int maxResults)
    {
        var poiCategories = await this._mapsSearchClient.GetPointOfInterestCategoryTreeAsync(SearchLanguage.EnglishUsa);

        var schoolCategories = poiCategories.Value.Categories.Where(x => x.Name.Contains("School"));
        var schoolCategoryIds = schoolCategories.Select(x => x.Id).AsEnumerable().Cast<int>().Take(10);

        // Call Azure Maps API

        SearchAddressResult searchResult = await this._mapsSearchClient.SearchNearbyPointOfInterestAsync(new SearchNearbyPointOfInterestOptions()
        {
            Coordinates = new GeoPosition(longitude, latitude),
            Language = SearchLanguage.EnglishUsa,
            RadiusInMeters = radiusInMeters,
            CategoryFilter = schoolCategoryIds,
            Top = maxResults
        });

       return await this.GetSearchAddressResultAsStringAsync(searchResult);
    }

    public async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocation(string location)
    {
        var locationResult = await this._mapsSearchClient.SearchAddressAsync(location, new SearchAddressOptions()
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

    private async Task<List<string>> GetSearchAddressResultAsStringAsync(SearchAddressResult addressResult)
    {
        var resultList = new List<string>();
        foreach (var result in addressResult.Results)
        {
            resultList.Add(result.PointOfInterest.Name);
        }
        var tempResultString = string.Join(", ", resultList);
        return resultList;
    }
}
