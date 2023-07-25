
using System.Globalization;
using System.Text.Json;
using app.connectors;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using static System.Net.Mime.MediaTypeNames;

namespace app.skills;

/// <summary>
/// This class is used to create a skill for Azure Maps.
/// </summary>
public class MapsSkill
{
    private readonly IMappingConnector _mappingConnector;

    public MapsSkill(IMappingConnector mappingConnector)
    {
        _mappingConnector = mappingConnector;
    }

    [SKFunction("Gets a list of landmarks from a location")]
    [SKFunctionContextParameter(Name = "categorySearchStrings",
        Description = "A comma-separated list of landmark categories to search for")]
    [SKFunctionContextParameter(Name = "latitude", Description = "The latitude to search for landmarks")]
    [SKFunctionContextParameter(Name = "longitude", Description = "The longitude to search for landmarks")]
    [SKFunctionContextParameter(Name = "radius", Description = "The search radius in metres")]
    [SKFunctionContextParameter(Name = "maxResults", Description = "The maximum number of results to return")]
    public async Task<SKContext> GetLandmarksFromLatitudeAndLongitude(SKContext context)
    {
        context.Variables.TryGetValue("categorySearchStrings", out string? categorySearchStringsString);
        context.Variables.TryGetValue("latitude", out string? latitudeString);
        context.Variables.TryGetValue("longitude", out string? longitudeString);
        context.Variables.TryGetValue("radius", out string? radiusString);
        context.Variables.TryGetValue("maxResults", out string? maxResultsString);

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

        var radius = 2000;

        if (radiusString != null &&
            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
        {
            throw new ArgumentException("Radius must be an integer");
        }

        var maxResults = 100;

        if (maxResultsString != null &&
            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
        {
            throw new ArgumentException("Max results must be an integer");
        }

        var categorySearchStrings = categorySearchStringsString?.Split(",").ToList();

        if (categorySearchStrings == null || categorySearchStrings.Count == 0)
        {
            throw new ArgumentException("Category search strings must be provided");
        }

        var landmarks = await _mappingConnector.GetLandmarksAsync(categorySearchStrings, latitude, longitude, radius, maxResults);
        
        foreach (var categoryLandmarksResultSet in landmarks)
        {
            context.Variables["LandmarkCategory_"+categoryLandmarksResultSet.Category] = string.Join(",",categoryLandmarksResultSet.Landmarks);
        }

        return context;
    }


    [SKFunction("Gets a list of facilities from a location")]
    [SKFunctionContextParameter(Name = "latitude", Description = "The latitude to search for facilities")]
    [SKFunctionContextParameter(Name = "longitude", Description = "The longitude to search for facilities")]
    [SKFunctionContextParameter(Name = "radius", Description = "The search radius in metres")]
    [SKFunctionContextParameter(Name = "maxResults", Description = "The maximum number of results to return")]
    public async Task<string> GetFacilitiesFromLatitudeAndLongitude(SKContext context)
    {
        context.Variables.TryGetValue("latitude", out string? latitudeString);
        context.Variables.TryGetValue("longitude", out string? longitudeString);
        context.Variables.TryGetValue("radius", out string? radiusString);
        context.Variables.TryGetValue("maxResults", out string? maxResultsString);

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

        var radius = 2000;

        if (radiusString != null &&
            !int.TryParse(radiusString, NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
        {
            throw new ArgumentException("Radius must be an integer");
        }

        var maxResults = 100;

        if (maxResultsString != null &&
            !int.TryParse(maxResultsString, NumberStyles.Any, CultureInfo.InvariantCulture, out maxResults))
        {
            throw new ArgumentException("Max results must be an integer");
        }

        var facilities = await _mappingConnector.GetFacilitiesAsync(
            latitude,
            longitude,
            radius,
            maxResults);

        return JsonSerializer.Serialize(facilities);
    }
}