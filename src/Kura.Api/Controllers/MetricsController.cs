namespace Kura.Api.Controllers;

using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Métricas operacionais da API. Use para monitorar SLOs declarados no pitch.
/// Endpoint público — não requer autenticação para facilitar monitoramento externo.
/// </summary>
[ApiController]
[Route("metrics")]
[AllowAnonymous]
public class MetricsController(KuraDbContext db) : ControllerBase
{
    /// <summary>
    /// Retorna métricas básicas para SLO tracking: contagens de entidades, uptime e ambiente.
    /// </summary>
    /// <returns>Snapshot de métricas operacionais no momento da requisição.</returns>
    /// <response code="200">Métricas retornadas com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetMetrics()
    {
        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            uptimeSeconds = Environment.TickCount64 / 1000,
            totalClinicas = await db.Clinicas.IgnoreQueryFilters().CountAsync(),
            totalPets = await db.Pets.IgnoreQueryFilters().CountAsync(),
            totalEventos = await db.EventosClinicos.IgnoreQueryFilters().CountAsync(),
            totalTriagensLuna = await db.TriagensLuna.IgnoreQueryFilters().CountAsync(),
            ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }
}
