using Microsoft.AspNetCore.Mvc;

namespace Kura.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public IActionResult Get() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
