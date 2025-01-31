using Microsoft.AspNetCore.Builder;

namespace Microsoft.Greenlight.Shared.Interfaces;

/// <summary>
/// Defines a contract for endpoint definitions.
/// </summary>
public interface IEndpointDefinition
{
    /// <summary>
    /// Defines the endpoints for the given web application.
    /// </summary>
    /// <param name="app">The web application to define endpoints for.</param>
    void DefineEndpoints(WebApplication app);
}
