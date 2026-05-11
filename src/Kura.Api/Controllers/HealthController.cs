using Microsoft.AspNetCore.Mvc;

namespace Kura.Api.Controllers;

/// <summary>
/// Health check da API. Endpoint público para monitoramento de disponibilidade.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Verifica se a API está operacional.
    /// </summary>
    /// <returns>Status "ok" e timestamp UTC atual.</returns>
    /// <response code="200">API operacional.</response>
    [HttpGet]
    [ProducesResponseType(200)]
    public IActionResult Get() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
