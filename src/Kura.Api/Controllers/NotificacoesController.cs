namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Notificacao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento de notificações da clínica autenticada.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly INotificacaoService _service;
    private readonly IClinicaContext _clinicaContext;

    public NotificacoesController(INotificacaoService service, IClinicaContext clinicaContext)
    {
        _service = service;
        _clinicaContext = clinicaContext;
    }

    /// <summary>
    /// Lista notificações da clínica, com filtro opcional para exibir apenas as não lidas.
    /// </summary>
    /// <param name="apenasNaoLidas">Quando true, retorna apenas notificações não lidas (opcional).</param>
    /// <returns>Lista de notificações da clínica.</returns>
    /// <response code="200">Notificações retornadas com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificacaoResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool? apenasNaoLidas)
    {
        var result = await _service.GetAllByClinicaAsync(_clinicaContext.IdClinica, apenasNaoLidas);
        return Ok(result);
    }

    /// <summary>
    /// Marca uma notificação como lida.
    /// </summary>
    /// <param name="id">Identificador da notificação.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Notificação marcada como lida.</response>
    /// <response code="404">Notificação não encontrada.</response>
    /// <response code="422">Notificação já foi marcada como lida anteriormente.</response>
    [HttpPatch("{id:long}/marcar-lida")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> MarcarLida(long id)
    {
        await _service.MarcarLidaAsync(id);
        return NoContent();
    }
}
