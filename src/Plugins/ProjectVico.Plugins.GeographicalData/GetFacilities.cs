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
    public async Task<HttpResponseData> GetLatitudeAndLongitudeForLocationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        string? location = req.Query["query"];

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location must be provided");
        }

        var responseDocument = await this._mappingConnector.GetLatitudeAndLongitudeForLocation(location);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(responseDocument));
        return response;
    }


    [Function("GetFacilitiesByLatitudeAngLongitude")]
    [OpenApiOperation(operationId: "GetFacilitiesByLatitudeAngLongitude", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of facilities from a location based on location's coordinates (<latitude> and <longitude>).")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetFacilitiesForLocationRequest), Required = true, Description = "JSON containing <latitude> and <longitude> as well as radius <radiusinmeters> and maxresults")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a comma separated list of locations")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<string> GetFacilitiesByLatitudeAngLongitudeAsync([HttpTrigger(AuthorizationLevel.Function, methods: "post")]
        HttpRequestData req,
        [FromBody] GetFacilitiesForLocationRequest bodyJson)
    {
        // These are default values if not set in the query string.
        var maxResults = 100;
        var radius = 2000;
        
        string? radiusString = bodyJson.RadiusInMeters.ToString(CultureInfo.InvariantCulture);
        string? maxResultsString = bodyJson.MaxResults.ToString(CultureInfo.InvariantCulture);

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

        var facilities = await this._mappingConnector.GetFacilities(
            latitude,
            longitude,
            radius,
            maxResults);

        var responseData = JsonSerializer.Serialize(facilities);

        return responseData;
    }
}
