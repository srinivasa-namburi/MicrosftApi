using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Connectors;
using Microsoft.Greenlight.Shared.Interfaces;


namespace Microsoft.Greenlight.Plugins.Default.EarthQuake.Endpoints;

public class EarthquakeEndpoint : IEndpointDefinition
{
    private readonly IEarthquakeConnector _earthquakeConnector;
    private readonly EarthquakePlugin _earthquakePlugin;

    public EarthquakeEndpoint(IEarthquakeConnector earthquakeConnector, EarthquakePlugin earhquakePlugin)
    {
        _earthquakeConnector = earthquakeConnector;
        _earthquakePlugin = earhquakePlugin;
    }

    public void DefineEndpoints(WebApplication app)
    {
        app.MapGet("/earthquakesbylatitudeandlongitude", _earthquakePlugin.GetEarthquakesByLatitudeAndLongitude)
            .WithName("GetEarthquakesByLatitudeAngLongitude")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                RequestBody = null,
                Description = "Gets a list of earthquakes from a location based on location's coordinates (latitude and longitude). If searching for an address, please use the function GetEarthquakesByAddress instead. If no minMagnitude is supplied, it is set to '3.5' by default.",
                Parameters = new List<OpenApiParameter>
                {
                    new()
                    {
                        Name = "latitude",
                        In = ParameterLocation.Query,
                        Required = true,
                        Description = "Latitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees",
                        Schema = new OpenApiSchema
                        {
                            Type = "number"
                        }
                    },
                    new()
                    {
                        Name = "longitude",
                        In = ParameterLocation.Query,
                        Required = true,
                        Description = "Longitude of the location to search for earthquakes represented as a float. Decimal from -180 to 180 degrees.",
                        Schema = new OpenApiSchema
                        {
                            Type = "number"
                        }
                    },
                    new()
                    {
                        Name = "maxRadiusKm",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The radius/area in kilometers to search for earthquakes from the supplied geographical coordinate (latitude and longitude). Integer from 0 to 20001.6km.",
                        Schema = new OpenApiSchema
                        {
                            Type = "integer"
                        }
                    },
                    new()
                    {
                        Name = "maxResults",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The max number of results to return. Cannot be more than 100, must be an integer.",
                        Schema = new OpenApiSchema
                        {
                            Type = "integer"
                        }
                    },
                    new()
                    {
                        Name = "minMagnitude",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The minimum magnitude of earthquakes to include from the supplied geograhical coordinate (latitude and longitude). Decimal.",
                        Schema = new OpenApiSchema
                        {
                            Type = "number"
                        }
                    },
                    new()
                    {
                        Name = "minDateString",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The beginning of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD",
                        Schema = new OpenApiSchema
                        {
                            Type = "string"
                        }
                    },
                    new()
                    {
                        Name = "maxDateString",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The end of the period fo time to search for earthquakes, formatted as a date in the format YYYY-MM-DD",
                        Schema = new OpenApiSchema
                        {
                            Type = "string"
                        }
                    },
                    new()
                    {
                        Name = "sortOrder",
                        In = ParameterLocation.Query,
                        Required = false,
                        Description = "The sort order of the returned results. Can be 'time' for descending time, 'time-asc' for ascending time, 'magnitude' for descending magnitude or 'magnitude-asc' for ascending magnitude. By default or if not specified, uses 'time' for the most recent first and sorted by time in a descending order.",
                        Schema = new OpenApiSchema
                        {
                            Type = "string"
                        }
                    }
                }
            });
    }
}
