namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/agenda")]
[Authorize]
public class AgendaController(IAgendaService agendaService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AgendaResponseDto), 200)]
    public async Task<IActionResult> GetAgenda(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim,
        [FromQuery] long? veterinarioId = null)
    {
        var result = await agendaService.GetAgendaAsync(dataInicio, dataFim, veterinarioId);
        return Ok(result);
    }

    [HttpPatch("~/api/v1/agendamentos/{id:long}/status")]
    [ProducesResponseType(typeof(AgendamentoItemDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> AtualizarStatus(long id, [FromBody] AtualizarStatusAgendamentoDto dto)
    {
        var result = await agendaService.AtualizarStatusAsync(id, dto);
        return Ok(result);
    }
}
