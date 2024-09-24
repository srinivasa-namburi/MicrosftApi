using System.ComponentModel;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Plugins.Default.GeographicalData.Connectors;
using ProjectVico.V2.Plugins.Default.GeographicalData.Models;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Plugins.Default.GeographicalData;

public class FacilitiesPlugin : IPluginImplementation
{
    private readonly IMappingConnector _mappingConnector;
    private readonly AzureFileHelper _fileHelper;

    public FacilitiesPlugin(IMappingConnector mappingConnector, AzureFileHelper fileHelper)
    {
        _mappingConnector = mappingConnector;
        _fileHelper = fileHelper;
    }

    // This method fetches latitude and longitude for a given address.
    [KernelFunction("GetLatLongForAddress")]
    [Description("Gets the latitude and longitude for an address.")]
    public async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatLongForAddressAsync(
        [Description("The address to search for latitude and longitude. Must be a string.")]
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));
        }

        var response = await _mappingConnector.GetLatitudeAndLongitudeForLocation(query);
        if (response == null)
        {
            throw new InvalidOperationException("Unable to retrieve location data.");
        }

        return response;
    }

    // This method retrieves detailed information about facilities within a certain radius of the given latitude and longitude.
    [KernelFunction("GetDetailedFacilitiesForLatLong")]
    [Description("Gets a list of facilities based on latitude and longitude.")]
    public async Task<List<FacilityDetail>> GetDetailedFacilitiesForLatLongAsync(

        [Description("The latitude of the location to search for facilities. Must be a float. Decimal from -90 to 90 degrees.")]
        double latitude,
        [Description("The longitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.")]
        double longitude,
        [Description("The radius/area in kilometers to search for facilities from the supplied geographical coordinate (latitude and longitude). Integer from 0 to 1000 km")]
        int radius,
        [Description("The max number of results to return. Cannot be more than 100, must be an integer.")]
        int maxResults,
        [Description("The category of facilities to search for. Must be a string.")]
        string categorySearchTerm)
    {
        ValidateCoordinates(latitude, longitude);

        if (radius <= 0)
        {
            throw new ArgumentException("Radius must be greater than 0.", nameof(radius));
        }

        if (maxResults <= 0 || maxResults > 100)
        {
            throw new ArgumentException("MaxResults must be between 1 and 100.", nameof(maxResults));
        }

        var facilities = await _mappingConnector.GetDetailedFacilitiesForCategoryName(
            latitude, longitude, radius, maxResults, categorySearchTerm ?? "School");

        return facilities ?? new List<FacilityDetail>();
    }

    [KernelFunction("GetFacilitiesByAddress")]
    [Description("Gets a list of facilities based on an address.")]
    public async Task<List<FacilityDetail>> GetFacilitiesByAddressAsync(
        [Description("The address to search for facilities. Must be a string.")]
        string address,
        [Description("The radius/area in kilometers to search for facilities from the supplied geographical coordinate (latitude and longitude). Integer from 0 to 1000 km")]
        int radius,
        [Description("The max number of results to return. Cannot be more than 100, must be an integer.")]
        int maxResults,
        [Description("The category of facilities to search for. Must be a string.")]
        string categorySearchTerm)
    {
        var latLongResponse = await GetLatLongForAddressAsync(address);

        var latitudeString = latLongResponse.Latitude;
        var longitudeString = latLongResponse.Longitude;


        if (latLongResponse.Latitude == 0 || latLongResponse.Longitude == 0)
        {
            throw new ArgumentException("Latitude and longitude must be provided");
        }

        if (radius == 0)
        {
            radius = 5000;
        }

        if (maxResults == 0)
        {
            maxResults = 100;
        }

        if (string.IsNullOrEmpty(categorySearchTerm))
        {
            categorySearchTerm = "School";
        }


        return await GetDetailedFacilitiesForLatLongAsync(latLongResponse.Latitude, latLongResponse.Longitude,
            radius, maxResults, categorySearchTerm);
    }

    // This method generates an image of a map with given latitude and longitude. The image is stored in blob storage and a link to the image is returned.
    [KernelFunction("GetMapImageLinkForLatLongAsync")]
    [Description("Gets the relative url path of a map image based on latitude and longitude. Do not add a host, http or https protocols or anything else to the path. Use as returned. ")]
    public async Task<string> GetMapImageLinkForLatLongAsync(

        [Description("The latitude of the location to search for facilities. Must be a float. Decimal from -90 to 90 degrees.")]
        double latitude,
        [Description("The longitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.")]
        double longitude,
        [Description("The zoom used when generating the map, determining how zoomed in on the latitude and logitude provided. There are 3 options: Close, Normal and Far. The default is Normal. \"Close\" option should be used when details are needed, while \"Far\" option should be used when an overview is needed")]
        MapZoomLevel mapZoomLevel = MapZoomLevel.Normal,
        [Description("The required width of the resulting map image in pixels. The default is 768 pixels wide.")]
        int imageWidth = 768,
        [Description("The required height of the image in pixels. The default is 512 pixels tall.")]
        int imageHeight = 512
        )
    {
        ValidateCoordinates(latitude, longitude);

        var mapStream = _mappingConnector.GetMapImageStream(latitude, longitude, ((int)mapZoomLevel), imageWidth, imageHeight);
        var fileName = $"map-{Guid.NewGuid()}.png";
        var mapLink = await _fileHelper.UploadFileToBlobAsync(mapStream, fileName, "document-assets", true);

        var asset = await _fileHelper.SaveFileInfoAsync(mapLink, "document-assets", fileName);
        
        var proxiedMapLink = _fileHelper.GetProxiedAssetBlobUrl(asset.Id.ToString());
        return proxiedMapLink;
    }

    public enum MapZoomLevel
    {
        Close = 5,
        Normal = 12,
        Far = 17
    }

    // Helper method to validate latitude and longitude.
    private void ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentException("Latitude must be between -90 and 90 degrees.", nameof(latitude));
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Longitude must be between -180 and 180 degrees.", nameof(longitude));
        }
    }
}
