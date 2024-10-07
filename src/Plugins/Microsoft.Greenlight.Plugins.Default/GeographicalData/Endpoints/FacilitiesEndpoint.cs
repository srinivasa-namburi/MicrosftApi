using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;
using Microsoft.Greenlight.Shared.Interfaces;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData.Endpoints;

public class FacilitiesEndpoint: IEndpointDefinition
{
    private readonly IMappingConnector _mappingConnector;
    private readonly FacilitiesPlugin _plugin;

    public FacilitiesEndpoint(IMappingConnector mappingConnector, FacilitiesPlugin plugin)
    {
        _mappingConnector = mappingConnector;
        _plugin = plugin;
    }

    public void DefineEndpoints(WebApplication app)
    {
        app.MapGet("/getlatitudeandlongitudeforaddress/{query}", async (string query) =>
                await _plugin.GetLatLongForAddressAsync(query))
            .WithName("GetLatitudeAndLongitudeForAddress")
            .WithOpenApi(x => new(x)

            {
                RequestBody = null,
                Description =
                    "Gets the latitude and longitude for an address in a json document. Set the \"query\" parameter to the address. Never use this function when coordinates are supplied by user.",
                Parameters = new List<OpenApiParameter>()
                {
                    new OpenApiParameter()
                    {
                        Name = "query",
                        Description =
                            "The query to submit to Azure maps. Must be an address consisting of street address, city and country. Ignore if input is only geographical coordinates",
                        Required = true,
                        In = ParameterLocation.Path,
                    }
                }
            });

        app.MapGet("/getfacilitiesbylatitudeandlongitude/{latitude}/{longitude}",
            async (double latitude, double longitude, int radius, int maxResults, string categorySearchTerm) =>
                await _plugin.GetDetailedFacilitiesForLatLongAsync(latitude, longitude, radius, maxResults, categorySearchTerm))
            .WithName("GetFacilitiesByLatitudeAngLongitude")
            .WithOpenApi(operation => new(operation)
            {
                RequestBody = null,
                Description = "Gets a list of facilities from a location based on location's coordinates (latitude and longitude). If searching for an address, please use the function GetFacilitiesByAddress instead. If no categorySearchTerm is supplied, it is set to 'School' by default.",
                Parameters = new List<OpenApiParameter>()
                {
                    new OpenApiParameter()
                    {
                        Name = "latitude",
                        Description = "The latitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new OpenApiParameter()
                    {
                        Name = "longitude",
                        Description = "The longitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new OpenApiParameter()
                    {
                        Name = "radius",
                        Description = "The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.",
                        Required = true,
                        In = ParameterLocation.Query,
                    },
                    new OpenApiParameter()
                    {
                        Name = "maxResults",
                        Description = "The max number of results to return. Cannot be more than 100, must be an integer.",
                        Required = true,
                        In = ParameterLocation.Query,
                    },
                    new OpenApiParameter()
                    {
                        Name = "categorySearchTerm",
                        Description = "The category/type of facility to search for, such as 'School', 'Healthcare', 'Elderly care', 'River', 'Lake', etc. Please capitalize the categorySearchTerm. Doing the search without categories is meaningless as the number of results would be far too great. For envirnomental reports, please limit yourself to facilities that are specifically thought to be vulnerable to radiaton or other fallout after a nuclear accident.",
                        Required = false,
                        In = ParameterLocation.Query,
                    }
                }
            });

        app.MapGet("/getfacilitiesbyaddress/{address}/{radius}/{maxResults}",
                async (string address, int radius, int maxResults, string categorySearchTerm)
                    => await _plugin.GetFacilitiesByAddressAsync(address, radius, maxResults, categorySearchTerm)
                )
            //.Produces<List<FacilityDetail>>(StatusCodes.Status200OK, "application/json")
            .WithName("GetFacilitiesByAddress")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                RequestBody = null,
                Description =
                    "Gets a list of facilities from a location based on an address. If searching for coordinates, please use the function GetFacilitiesByLatitudeAngLongitude instead. If no categorySearchTerm is supplied, it is set to 'School' by default.",
                Parameters = new List<OpenApiParameter>()
                {
                    new()
                    {
                        Name = "address",
                        Description = "The address of the location to search for facilities. Must be a string.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new()
                    {
                        Name = "radius",
                        Description =
                            "The radius/area in meters to search for facilities from the supplied geographical coordinate (latitude and longitude). Must be an integer, no fractional numbers.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new()
                    {
                        Name = "maxResults",
                        Description =
                            "The max number of results to return. Cannot be more than 100, must be an integer.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new()
                    {
                        Name = "categorySearchTerm",
                        Description =
                            "The category/type of facility to search for, such as 'School', 'Healthcare', 'Elderly care', 'River', 'Lake', etc. Please capitalize the categorySearchTerm. Doing the search without categories is meaningless as the number of results would be far too great. For envirnomental reports, please limit yourself to facilities that are specifically thought to be vulnerable to radiaton or other fallout after a nuclear accident.",
                        Required = false,
                        In = ParameterLocation.Query,
                    }
                }
            });

        app.MapGet("/getmapimagelinkforlatlong/{latitude}/{longitude}/{zoomLevel}", async (double latitude, double longitude, FacilitiesPlugin.MapZoomLevel zoom) =>
        {
            return app.Urls.First() + await _plugin.GetMapImageLinkForLatLongAsync(latitude, longitude);
            })
            .WithName("GetMapImageLinkForLatLong")
            .WithOpenApi(x => new(x)

            {
                RequestBody = null,
                Description =
                    "Gets the link of a map image based on latitude and longitude with a specified zoom level.",
                Parameters = new List<OpenApiParameter>()
                {
                    new OpenApiParameter()
                    {
                        Name = "latitude",
                        Description = "The latitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new OpenApiParameter()
                    {
                        Name = "longitude",
                        Description = "The longitude of the location to search for facilities. Must be a float. Decimal from -180 to 180 degrees.",
                        Required = true,
                        In = ParameterLocation.Path,
                    },
                    new OpenApiParameter()
                    {
                        Name = "zoomLevel",
                        Description = "The zoom used when generating the map, determining how zoomed in on the latitude and logitude provided. There are 3 options: Close, Normal and Far. The default is Normal. \"Close\" option should be used when details are needed, while \"Far\" option should be used when an overview is needed",
                        Required = true,
                        In = ParameterLocation.Path,
                    }
                }
            });
    }

   
}
