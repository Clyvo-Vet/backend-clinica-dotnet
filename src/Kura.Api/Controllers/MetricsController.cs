namespace Kura.Api.Controllers;

using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Métricas operacionais da API. Use para monitorar SLOs declarados no pitch.
/// </summary>
[ApiController]
[Route("metrics")]
public class MetricsController(KuraDbContext db, IClinicaContext clinicaContext) : ControllerBase
{
    /// <summary>
    /// TASK-45 — decisão de escopo deste endpoint (ver KURA_BACKLOG_FIX_3.md / CLAUDE.md,
    /// seção "Multi-tenancy no .NET"):
    ///
    /// Este GET raiz continua <see cref="AllowAnonymousAttribute"/> por exigência de
    /// rubrica FIAP (monitoramento externo sem credencial). Antes ele vazava volume
    /// cross-tenant "por acidente", chamando explicitamente o método do EF Core que
    /// ignora todos os query filters do <c>DbContext</c> (inclusive o de tenant) — e
    /// isso era redundante: o filtro de tenant em
    /// <c>KuraDbContext.ApplyTenantFilters</c> já DESLIGA por completo (não nega)
    /// quando <c>IdClinicaFiltro == null</c>, que é sempre o caso aqui, já que não há
    /// JWT. Ou seja: só tirar aquela chamada NÃO resolveria nada sozinho, o filtro de
    /// tenant já está inerte para uma chamada anônima.
    ///
    /// A correção real: o vazamento passa a ser uma decisão explícita e documentada, não
    /// um acidente. Os campos abaixo são globais DE PROPÓSITO (prefixo <c>ambiente*</c>),
    /// não existe mais nenhuma contagem por clínica aqui, e quem precisar de números
    /// isolados por clínica usa <see cref="GetMetricsClinica"/> (autenticado, abaixo).
    /// A chamada que ignorava os query filters foi removida daqui porque sem ela o
    /// filtro de <c>StAtiva</c> (soft delete) volta a valer nas queries abaixo — as
    /// contagens passam a refletir só registros ativos, mais correto para um SLO do
    /// que contar linhas soft-deletadas.
    /// </summary>
    /// <returns>Snapshot de métricas operacionais agregadas do ambiente inteiro.</returns>
    /// <response code="200">Métricas retornadas com sucesso.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetMetrics()
    {
        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            uptimeSeconds = Environment.TickCount64 / 1000,
            escopo = "ambiente",
            ambienteTotalClinicas = await db.Clinicas.CountAsync(),
            ambienteTotalPets = await db.Pets.CountAsync(),
            ambienteTotalEventos = await db.EventosClinicos.CountAsync(),
            ambienteTotalTriagensLuna = await db.TriagensLuna.CountAsync(),
            ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    /// <summary>
    /// Contrapartida autenticada de <see cref="GetMetrics"/> (TASK-45): contagens
    /// escopadas para a clínica do JWT, para quem precisa de números por clínica em vez
    /// do agregado global. Filtra explicitamente por <c>IdClinica</c> no predicado do
    /// LINQ — não delega ao query filter ambiente do EF — porque a intenção aqui é não
    /// depender silenciosamente de <c>IdClinicaFiltro</c> nunca vir null neste endpoint;
    /// o filtro do <c>DbContext</c> ainda se aplica por cima, como defesa em profundidade.
    /// </summary>
    /// <returns>Contagens de entidades da clínica autenticada.</returns>
    /// <response code="200">Métricas retornadas com sucesso.</response>
    /// <response code="401">Sem JWT válido.</response>
    [HttpGet("clinica")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMetricsClinica()
    {
        var idClinica = clinicaContext.IdClinica;

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            idClinica,
            totalPets = await db.Pets.CountAsync(p => p.IdClinica == idClinica),
            totalEventos = await db.EventosClinicos.CountAsync(e => e.IdClinica == idClinica),
            totalTriagensLuna = await db.TriagensLuna.CountAsync(t => t.IdClinica == idClinica)
        });
    }
}
