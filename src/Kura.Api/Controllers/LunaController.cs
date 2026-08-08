namespace Kura.Api.Controllers;

using Kura.Api.Filters;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Endpoints da IA Luna — triagem automática de pets via chatbot Python.
/// Relatório analítico acessível via JWT (uso clínico); os endpoints de escrita
/// (interactions/triage) são chamados pela Luna servidor-a-servidor, autenticados por
/// API Key (ver <see cref="LunaApiKeyAuthFilter"/>), nunca JWT de clínica.
/// </summary>
[ApiController]
[Route("api/v1/luna")]
public class LunaController(ILunaService lunaService) : ControllerBase
{
    /// <summary>
    /// Gera relatório agregado de triagens realizadas pela Luna em um intervalo de datas.
    /// Inclui total de triagens, distribuição de urgência e taxa de encaminhamento ao veterinário.
    /// </summary>
    /// <param name="dataInicio">Data inicial do relatório (inclusive).</param>
    /// <param name="dataFim">Data final do relatório (inclusive).</param>
    /// <returns>Relatório analítico de triagens no período.</returns>
    /// <response code="200">Relatório gerado com sucesso.</response>
    /// <response code="400">Intervalo de datas inválido.</response>
    [HttpGet("triagens/relatorio")]
    [Authorize]
    [ProducesResponseType(typeof(RelatorioTriagensDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> GerarRelatorio(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim)
    {
        var result = await lunaService.GerarRelatorioAsync(dataInicio, dataFim);
        return Ok(result);
    }

    /// <summary>
    /// TASK-67: registra uma interação de canal (WhatsApp/e-mail/SMS) recebida ou
    /// enviada pela IA Luna. Deriva ID_CLINICA a partir do tutor — ver LunaService
    /// para a decisão de negócio quando id_tutor vem null.
    /// </summary>
    /// <param name="dto">Dados da interação (shape espelha InteractionRequestDTO, Pydantic).</param>
    /// <returns>ID da interação registrada.</returns>
    /// <response code="201">Interação registrada com sucesso.</response>
    /// <response code="400">Payload malformado (ds_canal/ds_direcao fora do enum, ds_conteudo vazio).</response>
    /// <response code="404">id_tutor informado não corresponde a um tutor existente.</response>
    /// <response code="422">id_tutor ausente — não é possível derivar id_clinica.</response>
    [HttpPost("interactions")]
    [AllowAnonymous]
    [ServiceFilter(typeof(LunaApiKeyAuthFilter))]
    [ProducesResponseType(typeof(InteractionResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> RegistrarInteracao([FromBody] InteractionRequestDto dto)
    {
        var result = await lunaService.RegistrarInteracaoAsync(dto);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// TASK-67: registra o resultado de uma triagem de IA, ligada à interação que a
    /// originou (id_interacao).
    /// </summary>
    /// <param name="dto">Dados da triagem (shape espelha TriageRequestDTO, Pydantic).</param>
    /// <returns>ID da triagem registrada.</returns>
    /// <response code="201">Triagem registrada com sucesso.</response>
    /// <response code="400">Payload malformado (ds_urgencia fora do enum, campos obrigatórios ausentes).</response>
    /// <response code="404">id_interacao ou id_tutor informados não existem.</response>
    [HttpPost("triage")]
    [AllowAnonymous]
    [ServiceFilter(typeof(LunaApiKeyAuthFilter))]
    [ProducesResponseType(typeof(TriageResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> RegistrarTriagem([FromBody] TriageRequestDto dto)
    {
        var result = await lunaService.RegistrarTriagemAsync(dto);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
