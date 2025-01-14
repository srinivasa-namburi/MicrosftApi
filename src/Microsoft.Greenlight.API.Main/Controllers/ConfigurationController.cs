using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling configuration-related requests.
/// </summary>
public class ConfigurationController : BaseController
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
    /// </summary>
    /// <param name="serviceConfigurationOptionsSelector">The service configuration options selector.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    public ConfigurationController(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptionsSelector,
        IPublishEndpoint publishEndpoint
    )
    {
        _publishEndpoint = publishEndpoint;
        _serviceConfigurationOptions = serviceConfigurationOptionsSelector.Value;
    }

    /// <summary>
    /// Gets the Azure Maps key.
    /// </summary>
    /// <returns>The Azure Maps key.</returns>
    [HttpGet("azure-maps-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAzureMapsKey()
    {
        return Ok(_serviceConfigurationOptions.AzureMaps.Key);
    }

    /// <summary>
    /// Gets the document processes.
    /// </summary>
    /// <returns>A list of document process options.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpGet("document-processes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<List<DocumentProcessOptions>>]
    public ActionResult<List<DocumentProcessOptions>> GetDocumentProcesses()
    {
        var documentProcesses = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
        return Ok(documentProcesses);
    }

    /// <summary>
    /// Gets the feature flags.
    /// </summary>
    /// <returns>The feature flags options.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpGet("feature-flags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    [Produces<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions>]
    public ActionResult<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlags()
    {
        var featureFlags = _serviceConfigurationOptions.GreenlightServices.FeatureFlags;
        return Ok(featureFlags);
    }

    /// <summary>
    /// Restarts the workers.
    /// </summary>
    /// <returns>A confirmation message.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    /// </returns>
    [HttpPost("restart-workers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestartWorkers()
    {
        await _publishEndpoint.Publish(new RestartWorker(Guid.NewGuid()));
        return Ok("Restart command sent to all workers.");
    }
}
