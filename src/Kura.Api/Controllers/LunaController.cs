namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Endpoints da IA Luna — triagem automática de pets via chatbot Python.
/// Relatório analítico acessível via JWT (uso clínico).
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
}
