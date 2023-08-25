// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using ProjectVico.Plugins.GeographicalData.Connectors;
using Microsoft.OpenApi.Models;
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
        Description = "Gets the <latitude> and <longitude> for an address. Set the \"query\" parameter to the address. Never use this function when coordinates is supplied by user.")]
    [OpenApiParameter(name: "query", Description = "The query to submit to Azure maps. Must be an address consisting of street address, city and country. Ignore if input is only geographical coordinates", Required = true,
        In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK,
        contentType: "application/json",
        bodyType: typeof(GetLatitudeAndLongitudeForLocationResponse),
        Description = "Returns latitude and longitude as a json document providing a geographical location")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<GetLatitudeAndLongitudeForLocationResponse> GetLatitudeAndLongitudeForLocationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        string? location = req.Query["query"];

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location must be provided");
        }

        var responseDocument = await this._mappingConnector.GetLatitudeAndLongitudeForLocationAsync(location);
        return responseDocument;
    }


    [Function("GetFacilitiesByLatitudeAngLongitude")]
    [OpenApiOperation(operationId: "GetFacilitiesByLatitudeAngLongitude", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of facilities from a location based on location's <latitude> and <longitude> (coordinates). If latitude and longitude are specified as a json document, please split the parameters out first.")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetLatitudeAndLongitudeForLocationResponse), Required = true, Description = "JSON containing <latitude> and <longitude>")]
    [OpenApiParameter(name: "radiusInMeters", Type = typeof(int), Description = "The radius in meters to search for facilities. Must be an integer. If fractional, please round to nearest integer.", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "maxResults", Type = typeof(int), Description = "The maximum number of results to return. Maximum 100 per call.", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a comma separated list of locations")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetFacilitiesByLatitudeAngLongitudeAsync([HttpTrigger(AuthorizationLevel.Function, methods: "POST")] [FromBody] GetLatitudeAndLongitudeForLocationResponse bodyJson, HttpRequestData req)
    {

        // These are default values if not set in the query string.
        var maxResults = 100;
        var radius = 2000;
        

        string? radiusString = req.Query["radiusInMeters"];
        string? maxResultsString = req.Query["maxResults"];

        string? latitudeString = bodyJson.Latitude.ToString(CultureInfo.InvariantCulture);
        string? longitudeString = bodyJson.Longitude.ToString(CultureInfo.InvariantCulture);

        // Based on current culture, the decimal separator may be a comma or a period.
        // This is a problem for the SKContext, which expects a period.
        // So, we'll replace the comma with a period.
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

        var facilities = await this._mappingConnector.GetFacilitiesAsync(
            latitude,
            longitude,
            radius,
            maxResults);

        var responseData = JsonSerializer.Serialize(facilities);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync(responseData);

        return response;
    }
}
