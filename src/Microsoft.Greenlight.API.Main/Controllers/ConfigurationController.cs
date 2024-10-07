using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.API.Main.Controllers;

public class ConfigurationController : BaseController
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public ConfigurationController(IOptions<ServiceConfigurationOptions> serviceConfigurationOptionsSelector)
    {
        _serviceConfigurationOptions = serviceConfigurationOptionsSelector.Value;
    }

    [HttpGet("azure-maps-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAzureMapsKey()
    {
        return Ok(_serviceConfigurationOptions.AzureMaps.Key);
    }
    
    [HttpGet("document-processes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessOptions>>]
    public ActionResult<List<DocumentProcessOptions>> GetDocumentProcesses()
    {
        var documentProcesses = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
        return Ok(documentProcesses);
    }

    [HttpGet("feature-flags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions>]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlags()
    {
        var featureFlags = _serviceConfigurationOptions.GreenlightServices.FeatureFlags;
        return Ok(featureFlags);
    }
}
