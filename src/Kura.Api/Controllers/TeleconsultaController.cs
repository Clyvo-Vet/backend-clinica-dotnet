namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Teleconsulta;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gestão de salas de teleconsulta (Daily.co) vinculadas a um agendamento.
/// Exige consentimento de teleorientação (LGPD/CFMV 1.465/2022) registrado pelo tutor.
/// Não expõe geração de prescrição — vedado em teleconsulta pela resolução do CFMV.
/// </summary>
[ApiController]
[Route("api/v1/teleconsulta")]
[Authorize]
public class TeleconsultaController(ITeleconsultaService teleconsultaService) : ControllerBase
{
    /// <summary>
    /// Cria a sala de videochamada do agendamento (ou retorna a já existente, de forma idempotente).
    /// Se o provedor Daily.co falhar, retorna StFallbackManual=true para entrada de link externo manual.
    /// </summary>
    /// <param name="idAgendamento">Identificador do agendamento.</param>
    /// <response code="200">Sala criada/retornada com sucesso (ou fallback manual sinalizado).</response>
    /// <response code="404">Agendamento não encontrado.</response>
    /// <response code="422">Consentimento de teleorientação ausente ou não aceito.</response>
    [HttpPost("{idAgendamento:long}/sala")]
    [ProducesResponseType(typeof(TeleconsultaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> CriarSala(long idAgendamento)
    {
        var result = await teleconsultaService.CriarOuObterSalaAsync(idAgendamento);
        return Ok(result);
    }

    /// <summary>
    /// Retorna o estado atual da sala de videochamada do agendamento (sem criar uma nova).
    /// </summary>
    /// <param name="idAgendamento">Identificador do agendamento.</param>
    /// <response code="200">Estado da sala retornado com sucesso.</response>
    /// <response code="404">Agendamento não encontrado.</response>
    [HttpGet("{idAgendamento:long}/sala")]
    [ProducesResponseType(typeof(TeleconsultaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ObterSala(long idAgendamento)
    {
        var result = await teleconsultaService.ObterSalaAsync(idAgendamento);
        return Ok(result);
    }
}
