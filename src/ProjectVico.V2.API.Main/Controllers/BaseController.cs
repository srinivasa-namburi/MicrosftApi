using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace ProjectVico.V2.API.Main.Controllers;

[ApiController]
[Authorize(Roles = "DocumentGeneration")]
[RequiredScope("access_as_user")]
[Route("/api/[controller]")]
public abstract class BaseController : ControllerBase
{

}