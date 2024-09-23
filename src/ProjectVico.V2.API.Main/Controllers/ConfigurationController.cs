using HandlebarsDotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.API.Main.Controllers;

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
        var documentProcesses = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
        return Ok(documentProcesses);
    }

    [HttpGet("feature-flags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<ServiceConfigurationOptions.ProjectVicoServicesOptions.FeatureFlagsOptions>]
    public ActionResult<ServiceConfigurationOptions.ProjectVicoServicesOptions.FeatureFlagsOptions> GetFeatureFlags()
    {
        var featureFlags = _serviceConfigurationOptions.ProjectVicoServices.FeatureFlags;
        return Ok(featureFlags);
    }
}