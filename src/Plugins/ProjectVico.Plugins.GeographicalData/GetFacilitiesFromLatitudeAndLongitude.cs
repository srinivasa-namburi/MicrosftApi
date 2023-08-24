// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using ProjectVico.Plugins.GeographicalData.Connectors;
using Microsoft.OpenApi.Models;

namespace ProjectVico.Plugins.GeographicalData;

public class GetFacilitiesFromLatitudeAndLongitude
{
    private readonly IMappingConnector _mappingConnector;

    public GetFacilitiesFromLatitudeAndLongitude(IMappingConnector mappingConnector)
    {
        this._mappingConnector = mappingConnector;
    }

    [FunctionName("GetFacilitiesFromLatitudeAndLongitude")]
    [OpenApiOperation(operationId: "GetFacilitiesFromLatitudeAndLongitude", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of facilities from a location based on location's latitude and longitude")]
    [OpenApiParameter(name: "latitude", Description = "The latitude to search for facilities", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "longitude", Description = "The longitude to search for facilities", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "radiusInMeters", Description = "The radius in meters to search for facilities", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "maxResults", Description = "The maximum number of results to return. Maximum 100 per call.", Required = false, In = ParameterLocation.Query)]

    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a description of the format of a section.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync(
        [Microsoft.Azure.Functions.Worker.HttpTrigger(AuthorizationLevel.Function, "get")]
        HttpRequestData req)
    {

        // These are default values if not set in the query string.
        var maxResults = 100;
        var radius = 2000;
        
        string? latitudeString = req.Query["latitude"];
        string? longitudeString = req.Query["longitude"];
        string? radiusString = req.Query["radiusInMeters"];
        string? maxResultsString = req.Query["maxResults"];

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
