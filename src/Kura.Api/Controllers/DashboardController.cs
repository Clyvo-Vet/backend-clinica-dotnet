namespace Kura.Api.Controllers;

using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Dashboard operacional da clínica: resumo do dia, alertas e agendamentos recentes.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    /// <summary>
    /// Retorna o resumo operacional do dia atual: agendamentos, alertas e métricas rápidas.
    /// </summary>
    /// <returns>Resumo do dia atual da clínica.</returns>
    /// <response code="200">Resumo retornado com sucesso.</response>
    [HttpGet("hoje")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHoje()
    {
        var result = await _service.GetHojeAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retorna os alertas ativos da clínica (alertas de temperatura IoT e notificações pendentes).
    /// </summary>
    /// <returns>Lista de alertas ativos.</returns>
    /// <response code="200">Alertas retornados com sucesso.</response>
    [HttpGet("alertas")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetAlertas()
    {
        var result = await _service.GetAlertasAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retorna os agendamentos mais recentes da clínica para exibição no painel.
    /// </summary>
    /// <returns>Lista dos agendamentos recentes.</returns>
    /// <response code="200">Agendamentos retornados com sucesso.</response>
    [HttpGet("recentes")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetRecentes()
    {
        var result = await _service.GetRecentesAsync();
        return Ok(result);
    }
}
