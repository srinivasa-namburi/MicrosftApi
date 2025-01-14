using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Base controller class for API controllers.
/// </summary>
/// <remarks>
/// This class is abstract and provides common functionality for derived controllers.
/// </remarks>
[ApiController]
[Authorize(Roles = "DocumentGeneration")]
[RequiredScope("access_as_user")]
[Route("/api/[controller]")]
public abstract class BaseController : ControllerBase
{

}
