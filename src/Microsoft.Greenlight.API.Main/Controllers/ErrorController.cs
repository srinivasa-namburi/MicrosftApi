using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Greenlight.API.Main.Controllers;


/// <summary>
/// Controller for handling errors in the application.
/// </summary>
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : ControllerBase
{
    /// <summary>
    /// Handles errors during development.
    /// </summary>
    /// <param name="hostEnvironment">The hosting environment.</param>
    /// <returns>An <see cref="IActionResult"/> containing the error details.
    /// Produces Status Codes:
    ///     500 Internal Server Error: When exceptions occur
    /// </returns>
    [Route("/error-development")]
    public IActionResult HandleErrorDevelopment(
        [FromServices] IHostEnvironment hostEnvironment)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return NotFound();
        }

        var exceptionHandlerFeature =
            HttpContext.Features.Get<IExceptionHandlerFeature>()!;

        return Problem(
            detail: exceptionHandlerFeature.Error.StackTrace,
            title: exceptionHandlerFeature.Error.Message);
    }

    /// <summary>
    /// Handles errors in production.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> informing there has been an error.
    /// Produces Status Codes:
    ///     500 Internal Server Error: When exceptions occur
    /// </returns>
    [Route("/error")]
    public IActionResult HandleError() =>
        Problem();
}
