//// Copyright (c) Microsoft. All rights reserved.

//using System.Globalization;
//using System.Net;
//using System.Security.Cryptography;
//using System.Text.Json;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
//using Microsoft.OpenApi.Models;
//using ProjectVico.Plugins.GeographicalData.Connectors;
//using ProjectVico.Plugins.GeographicalData.Models;

//namespace ProjectVico.Plugins.GeographicalData;

//public class GetEarthqukes
//{
//    private readonly IEarthquakeConnector _earthquakeConnector;

//    public GetEarthqukes(IEarthquakeConnector earthquakeConnector)
//    {
//        this._earthquakeConnector = earthquakeConnector;
//    }

//    [Function("GetEarthquakesByLatitudeAngLongitude")]
//    [OpenApiOperation(operationId: "GetEarthquakesByLatitudeAngLongitude", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of earthquakes from a location based on location's coordinates (<latitude> and <longitude>). If searching for an address, please use the function GetEarthquakesByAddress instead. If no <minMagnitude> is supplied, it is set to '3.5' by default.")]
//    //[OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetEarthquakesForLocationRequest), Required = true, Description = "JSON containing <latitude> and <longitude> as well as radius <radiusinmeters> and maxresults")]
//    [OpenApiParameter(name: "maxResults", Description = "The max number of results to return. Cannot be more than 50, must be an integer.", Required = false, In = ParameterLocation.Query)]
//    [OpenApiParameter(name: "maxRadiusKm", Description = "The radius/area in meters to search for earthquakes from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.", Required = false, In = ParameterLocation.Query)]
//    [OpenApiParameter(name: "minMagnitude", Description = "The minimum magnitude of earthquakes to include from the supplied geograhical coordinate (latitude and longitude). Decimal.", Required = false, In = ParameterLocation.Query)]
//    [OpenApiParameter(name: "latitude", Description = "The latitude of the location to search for earthquakes. Use JsonPath skill to get if necessary", Required = true, In = ParameterLocation.Query)]
//    [OpenApiParameter(name: "longitude", Description = "The longitude of the location to search for earthquakes. Use JsonPath skill to get if necessary", Required = true, In = ParameterLocation.Query)]
//    [OpenApiParameter(name: "mindate", Description="The beginning of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD", Required = false, In=ParameterLocation.Query)]
//    [OpenApiParameter(name: "maxdate", Description="The end of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD", Required = false, In=ParameterLocation.Query)]
//    [OpenApiParameter(name: "sortorder", Description= "The sort order of the returned results. Can be 'time' for descending time, 'time-asc' for ascending time, 'magnitude' for descending magnitude or 'magnitude-asc' for ascending magnitude. By default or if not specified, use 'time' for the most recent first and sorted by time in a descending order.", Required = false, In=ParameterLocation.Query)]
//    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<EarthquakeDetail>),
//        Description = "Returns a list of EarthquakeDetail objects")]
//    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]

//    public async Task<HttpResponseData> GetEarthquakesByLatitudeAngLongitudeAsync([HttpTrigger(AuthorizationLevel.Anonymous, methods: "get")]
//        HttpRequestData req)
//    {
//        // These are default values if not set in the query string.
//        var maxResults = 50;
//        var radius = 20;
//        var minMagnitude = 3.5;
//        var sortorder = "time";

//        string? mindateString = req.Query["mindate"];
//        string? maxdateString = req.Query["maxdate"];
//        string? radiusString = req.Query["maxRadiusKm"];
//        string? maxResultsString = req.Query["maxResults"];
//        string? minMagnitudeString = req.Query["minMagnitude"];

//        if (string.IsNullOrEmpty(minMagnitudeString))
//        {
//            minMagnitudeString = minMagnitude.ToString(CultureInfo.InvariantCulture);
//        }

//        string? latitudeString = req.Query["latitude"];
//        string? longitudeString = req.Query["longitude"];
//        string? sortorderString = req.Query["sortorder"];

//        if (!string.IsNullOrEmpty(sortorderString))
//        {
//            // Sort Order must be one of time, time-asc, magnitude or magnitude-asc.
//            if (sortorderString != "time" &&
//                sortorderString != "time-asc" &&
//                sortorderString != "magnitude" &&
//                sortorderString != "magnitude-asc")
//            {
//                throw new ArgumentException("Sort order must be one of time, time-asc, magnitude or magnitude-asc");
//            }
//            else
//            {
//                sortorder = sortorderString;
//            }
//        }
//        else
//        {
//            sortorder = "time";
//        }

//        DateOnly mindate;
//        if (!string.IsNullOrEmpty(mindateString))
//        {
//            if (!DateOnly.TryParse(mindateString, out mindate))
//            {
//                throw new ArgumentException("Mindate must be a date in the format YYYY-MM-DD");
//            }
//        }
//        else
//        {
//            // If no minimum date is specfied, search the last 5 years
//            mindate = DateOnly.FromDateTime(DateTime.Now.AddYears(-5));
//        }

//        DateOnly maxdate;
//        if (!string.IsNullOrEmpty(maxdateString))
//        {
//            if (!DateOnly.TryParse(maxdateString, out maxdate))
//            {
//                throw new ArgumentException("Maxdate must be a date in the format YYYY-MM-DD");
//            }
//        }
//        else
//        {
//            // If no maximum date is specified, set it to today
//            maxdate = DateOnly.FromDateTime(DateTime.UtcNow);
//        }

//        // This fixes potential codepage errors for languages where "," is the decimal separator instead of "."
//        latitudeString = latitudeString?.Replace(",", ".");
//        longitudeString = longitudeString?.Replace(",", ".");
//        minMagnitudeString = minMagnitudeString?.Replace(",", ".");

//        if (!double.TryParse(latitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out var latitude))
//        {
//            throw new ArgumentException("Latitude must be a double");
//        }

//        if (!double.TryParse(longitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out var longitude))
//        {
//            throw new ArgumentException("Longitude must be a double");
//        }

//        if (!double.TryParse(minMagnitudeString, NumberStyles.Any, CultureInfo.InvariantCulture, out minMagnitude))
//        {
//            throw new ArgumentException("Magnitude must be a double");
//        }

//        if (radiusString != null &&
//            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
//        {
//            throw new ArgumentException("Radius must be an integer");
//        }

//        if (maxResultsString != null &&
//            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
//        {
//            throw new ArgumentException("Max results must be an integer");
//        }

//        var responseData = await this.GetDetailedEarthquakesForLatLongAsync(latitude, longitude, radius, maxResults, minMagnitude, mindate, maxdate, sortorder);

//        var response = req.CreateResponse(HttpStatusCode.OK);
//        await response.WriteAsJsonAsync(responseData);
//        return response;
//    }

   
//    private async Task<List<EarthquakeDetail>> GetDetailedEarthquakesForLatLongAsync(double latitude, double longitude,
//        int radius, int maxresults, double minmagnitude, DateOnly minDate, DateOnly maxDate, string sortOrder = "time")
//    {
//        var earthquakes = await this._earthquakeConnector.GetDetailedEarthquakesForMinMagnitude(
//                                 latitude,
//                                longitude,
//                                radius,
//                                maxresults,
//                                minmagnitude,
//                                 minDate,
//                                 maxDate,
//                                 sortOrder);

//        return earthquakes;
//    }
//}
