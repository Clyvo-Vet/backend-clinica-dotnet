namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/luna")]
public class LunaController(ILunaService lunaService) : ControllerBase
{
    /// <summary>
    /// Relatório agregado de triagens em um intervalo. Acesso via JWT (uso clínico).
    /// </summary>
    [HttpGet("triagens/relatorio")]
    [Authorize]
    [ProducesResponseType(typeof(RelatorioTriagensDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GerarRelatorio(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim)
    {
        var result = await lunaService.GerarRelatorioAsync(dataInicio, dataFim);
        return Ok(result);
    }
}
