namespace Kura.Api.Controllers;

using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet("hoje")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHoje()
    {
        var result = await _service.GetHojeAsync();
        return Ok(result);
    }

    [HttpGet("alertas")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetAlertas()
    {
        var result = await _service.GetAlertasAsync();
        return Ok(result);
    }

    [HttpGet("recentes")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetRecentes()
    {
        var result = await _service.GetRecentesAsync();
        return Ok(result);
    }
}
