// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using ProjectVico.Plugins.GeographicalData.Connectors;
using ProjectVico.Plugins.GeographicalData.Models;

namespace ProjectVico.Plugins.GeographicalData;

public class GetFacilities
{
    private readonly IMappingConnector _mappingConnector;

    public GetFacilities(IMappingConnector mappingConnector)
    {
        this._mappingConnector = mappingConnector;
    }

    [Function(name: "GetLatitudeAndLongitudeForAddress")]
    [OpenApiOperation(operationId: "GetLatitudeAndLongitudeForAddress", tags: new[] { "ExecuteFunction" },
        Description = "Gets the <latitude> and <longitude> for an address in a json document. Set the \"query\" parameter to the address. Never use this function when coordinates are supplied by user.")]
    [OpenApiParameter(name: "query", Description = "The query to submit to Azure maps. Must be an address consisting of street address, city and country. Ignore if input is only <geographical coordinates>", Required = true,
        In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK,
        contentType: "application/json",
        bodyType: typeof(GetLatitudeAndLongitudeForLocationResponse),
        Description = "Coordinates split into latitude and longitude. Formatted as a json document.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForAddressAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        string? location = req.Query["query"];

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location must be provided");
        }

        return await this.GetLatLongForAddressAsync(location);
    }

    [Function("CreateGetFacilitiesForLocationRequestFromGetLatitudeAndLongitudeForLocationResponse")]
    [OpenApiOperation(operationId: "CreateGetFacilitiesForLocationRequestFromGetLatitudeAndLongitudeForLocationResponse", tags: new[] { "ExecuteFunction" }, Description = "Creates a GetFacilitiesForLocationRequest from a GetLatitudeAndLongitudeForLocationResponse containing <latitude> and <longitude>")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetLatitudeAndLongitudeForLocationResponse),
        Required = true, Description = "JSON containing <latitude> and <longitude> properties. Prefill these properties with <latitude> and <longitude>")]
    [OpenApiParameter(name: "maxResults", Description = "The max number of results to return. Cannot be more than 100, must be an integer.", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "radiusInMeters", Description = "The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
        bodyType: typeof(GetFacilitiesForLocationRequest), Description = "Returns a GetFacilitiesForLocationRequest")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<GetFacilitiesForLocationRequest>
        CreateGetFacilitiesForLocationRequestFromGetLatitudeAndLongitudeForLocationResponseAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "post")]
            HttpRequestData req,
            [FromBody] GetLatitudeAndLongitudeForLocationResponse bodyJson)
    {

        var maxResults = 100;
        var radius = 2000;

        string? radiusString = req.Query["radiusInMeters"];
        string? maxResultsString = req.Query["maxResults"];

        if (radiusString != null &&
            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
        {
            throw new ArgumentException("Radius must be an integer");
        }
        if (maxResultsString != null &&
            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
        {
            throw new ArgumentException("Max results must be an integer");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var getFacilitiesForLocationRequest = new GetFacilitiesForLocationRequest
        {
            Latitude = bodyJson.Latitude,
            Longitude = bodyJson.Longitude,
            MaxResults = maxResults,
            RadiusInMeters = radius
        };

        return getFacilitiesForLocationRequest;
    }


    [Function("GetFacilitiesByLatitudeAngLongitude")]
    [OpenApiOperation(operationId: "GetFacilitiesByLatitudeAngLongitude", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of facilities from a location based on location's coordinates (<latitude> and <longitude>). If searching for an address, please use the function GetFacilitiesByAddress instead. If no <categorySearchTerm> is supplied, it is set to 'School' by default.")]
    //[OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetFacilitiesForLocationRequest), Required = true, Description = "JSON containing <latitude> and <longitude> as well as radius <radiusinmeters> and maxresults")]
    [OpenApiParameter(name: "maxResults", Description = "The max number of results to return. Cannot be more than 100, must be an integer.", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "radiusInMeters", Description = "The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "latitude", Description = "The latitude of the location to search for facilities. Use JsonPath skill to get if necessary", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "longitude", Description = "The longitude of the location to search for facilities. Use JsonPath skill to get if necessary", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "categorySearchTerm", Description = "The category/type of facility to search for, such as 'School', 'Healthcare', 'Elderly care', 'River', 'Lake', etc. Please capitalize the categorySearchTerm.", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<FacilityDetail>),
        Description = "Returns a list of FacilityDetail objects with name, address, distance from search point and categories of each facility found")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]

    public async Task<HttpResponseData> GetFacilitiesByLatitudeAngLongitudeAsync([HttpTrigger(AuthorizationLevel.Anonymous, methods: "get")]
        HttpRequestData req)
    {
        // These are default values if not set in the query string.
        var maxResults = 100;
        var radius = 2000;

        string? radiusString = req.Query["radiusInMeters"];
        string? maxResultsString = req.Query["maxResults"];
        string? latitudeString = req.Query["latitude"];
        string? longitudeString = req.Query["longitude"];
        string? categorySearchTerm = req.Query["categorySearchTerm"];

        if (string.IsNullOrEmpty(categorySearchTerm))
        {
            categorySearchTerm = "School";
        }

        latitudeString = latitudeString?.Replace(",", ".");
        longitudeString = longitudeString?.Replace(",", ".");

        if (!double.TryParse(latitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out var latitude))
        {
            throw new ArgumentException("Latitude must be a double");
        }

        if (!double.TryParse(longitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out var longitude))
        {
            throw new ArgumentException("Longitude must be a double");
        }

        if (radiusString != null &&
            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
        {
            throw new ArgumentException("Radius must be an integer");
        }

        if (maxResultsString != null &&
            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
        {
            throw new ArgumentException("Max results must be an integer");
        }

        var responseData = await this.GetDetailedFacilitiesForLatLongAsync(latitude, longitude, radius, maxResults, categorySearchTerm);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(responseData);
        return response;
    }

    [Function("GetFacilitiesByAddress")]
    [OpenApiOperation(operationId: "GetFacilitiesByAddress", tags: new[] { "ExecuteFunction" },
        Description =
            "Gets a list of facilities from a location based on an address. Limit the type of facility to those of type <categorySearchTerm>. If no <categorySearchTerm> is supplied, it is set to 'School' by default.")]
    [OpenApiParameter(name: "address", Description = "The address or location name to find facilities for",
        Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "maxResults",
        Description = "The max number of results to return. Cannot be more than 100, must be an integer. Set to 100 by default.",
        Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "radiusInMeters",
        Description =
            "The radius/area in meters to search for '<categorySearchTerm>' type of facilities from the supplied address. Must be an integer, no fractional numbers. Default 5km radius.",
        Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "categorySearchTerm",
        Description =
            "The category/type of facility to search for, such as 'School', 'Hospital', 'Elderly care', 'River', 'Lake', etc. Please capitalize the categorySearchTerm.",
        Required = false, In = ParameterLocation.Query)]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string),
    //    Description = "Returns a comma separated list of locations")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<FacilityDetail>),
        Description = "Returns a list of FacilityDetail objects with name, address, distance from search point and categories of each facility found")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetFacilitiesByAddressAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get")] HttpRequestData req)
    {
        var latLongResponse = await this.GetLatLongForAddressAsync(req.Query["address"]!);

        var latitudeString = latLongResponse.Latitude;
        var longitudeString = latLongResponse.Longitude;
        var radiusString = req.Query["radiusInMeters"];
        var maxResultsString = req.Query["maxResults"];

        int radius = 5000;
        int maxResults = 100;

        var categorySearchTerm = req.Query["categorySearchTerm"] ?? "School";

        if (!double.TryParse(latitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double latitude))
        {
            throw new ArgumentException("Latitude must be a double");
        }

        if (!double.TryParse(longitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double longitude))
        {
            throw new ArgumentException("Longitude must be a double");
        }

        if (radiusString != null &&
            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
        {
            throw new ArgumentException("Radius must be an integer");
        }

        if (maxResultsString != null &&
            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
        {
            throw new ArgumentException("Max results must be an integer");
        }

        var facilities = await this.GetDetailedFacilitiesForLatLongAsync(
                                 latitude,
                                  longitude,
                                  radius,
                                  maxResults,
                                  categorySearchTerm);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(facilities);
        return response;
    }


    private async Task<string> GetFacilitiesForLatLongAsync(double latitude, double longitude, int radius, int maxResults, string categorySearchTerm)
    {
        var facilities = await this._mappingConnector.GetFacilitiesForCategoryname(
                      latitude,
                      longitude,
                      radius,
                      maxResults,
                      categorySearchTerm);

        var responseData = JsonSerializer.Serialize(facilities);
        return responseData;
    }

    private async Task<List<FacilityDetail>> GetDetailedFacilitiesForLatLongAsync(double latitude, double longitude,
        int radius, int maxResults, string categorySearchTerm)
    {
        var facilities = await this._mappingConnector.GetDetailedFacilitiesForCategoryName(
                                 latitude,
                                                      longitude,
                                                      radius,
                                                      maxResults,
                                                      categorySearchTerm);

        return facilities;
    }

    private async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatLongForAddressAsync(string location)
    {
        var responseDocument = await this._mappingConnector.GetLatitudeAndLongitudeForLocation(location);
        return responseDocument;
    }
}
