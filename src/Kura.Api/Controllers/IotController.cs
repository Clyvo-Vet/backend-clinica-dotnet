namespace Kura.Api.Controllers;

using Kura.Application.DTOs.AlertaTemperatura;
using Kura.Application.DTOs.DispositivoIot;
using Kura.Application.DTOs.Iot;
using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Api.Filters;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/iot")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class IotController : ControllerBase
{
    private readonly ILeituraTemperaturaService _leituraService;
    private readonly IDispositivoIotService _dispositivoService;
    private readonly IAlertaTemperaturaService _alertaService;

    public IotController(
        ILeituraTemperaturaService leituraService,
        IDispositivoIotService dispositivoService,
        IAlertaTemperaturaService alertaService)
    {
        _leituraService = leituraService;
        _dispositivoService = dispositivoService;
        _alertaService = alertaService;
    }

    [HttpPost("leituras")]
    [ProducesResponseType(typeof(LeituraTemperaturaResponseDto), 201)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> RegistrarLeitura([FromBody] LeituraTemperaturaCreateDto dto)
    {
        var result = await _leituraService.RegistrarLeituraAsync(dto);
        return CreatedAtAction(nameof(RegistrarLeitura), new { id = result.Id }, result);
    }

    [HttpGet("dispositivos")]
    [ProducesResponseType(typeof(IEnumerable<DispositivoIotResponseDto>), 200)]
    public async Task<IActionResult> GetDispositivos()
    {
        var result = await _dispositivoService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("dispositivos/{id:long}/leituras")]
    [ProducesResponseType(typeof(IEnumerable<LeituraTemperaturaResponseDto>), 200)]
    public async Task<IActionResult> GetLeituras(
        long id,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var result = await _leituraService.GetByDispositivoAsync(id, dataInicio, dataFim);
        return Ok(result);
    }

    [HttpGet("dispositivos/{id:long}/status")]
    [ProducesResponseType(typeof(DispositivoStatusDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStatus(long id)
    {
        var result = await _dispositivoService.GetStatusAsync(id);
        return Ok(result);
    }

    [HttpGet("alertas")]
    [ProducesResponseType(typeof(IEnumerable<AlertaTemperaturaResponseDto>), 200)]
    public async Task<IActionResult> GetAlertas([FromQuery] bool? resolvido)
    {
        var apenasNaoResolvidos = resolvido == false ? true : (bool?)null;
        var result = await _alertaService.GetAllAsync(apenasNaoResolvidos);
        return Ok(result);
    }
}
