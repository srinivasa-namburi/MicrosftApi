using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.API.Main.Controllers;

public class ConfigurationController : BaseController
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public ConfigurationController(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptionsSelector,
        IPublishEndpoint publishEndpoint
        )
    {
        _publishEndpoint = publishEndpoint;
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

    [HttpPost("restart-workers")]
    public async Task<IActionResult> RestartWorkers()
    {
        await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
        return Ok("Restart command sent to all workers.");
    }
}
