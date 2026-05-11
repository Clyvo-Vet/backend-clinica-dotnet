namespace Kura.Api.Controllers;

using Kura.Application.DTOs.AlertaTemperatura;
using Kura.Application.DTOs.DispositivoIot;
using Kura.Application.DTOs.Iot;
using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Api.Filters;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Ingestão e consulta de dados de dispositivos IoT (sensores de temperatura).
/// Autenticação via API Key no header X-Api-Key (uso exclusivo de dispositivos ESP32 e Luna).
/// </summary>
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

    /// <summary>
    /// Ingere uma leitura de temperatura enviada por um dispositivo IoT.
    /// Gera alerta automático se o valor exceder o threshold configurado.
    /// </summary>
    /// <param name="dto">Leitura de temperatura com ID do dispositivo e timestamp.</param>
    /// <returns>Leitura persistida com ID gerado.</returns>
    /// <response code="201">Leitura registrada com sucesso.</response>
    /// <response code="404">Dispositivo não encontrado.</response>
    /// <response code="422">Valor de temperatura fora do intervalo permitido.</response>
    [HttpPost("leituras")]
    [ProducesResponseType(typeof(LeituraTemperaturaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> RegistrarLeitura([FromBody] LeituraTemperaturaCreateDto dto)
    {
        var result = await _leituraService.RegistrarLeituraAsync(dto);
        return CreatedAtAction(nameof(RegistrarLeitura), new { id = result.Id }, result);
    }

    /// <summary>
    /// Lista todos os dispositivos IoT cadastrados.
    /// </summary>
    /// <returns>Lista de dispositivos IoT.</returns>
    /// <response code="200">Dispositivos retornados com sucesso.</response>
    [HttpGet("dispositivos")]
    [ProducesResponseType(typeof(IEnumerable<DispositivoIotResponseDto>), 200)]
    public async Task<IActionResult> GetDispositivos()
    {
        var result = await _dispositivoService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retorna o histórico de leituras de um dispositivo, com filtro opcional por período.
    /// </summary>
    /// <param name="id">Identificador do dispositivo IoT.</param>
    /// <param name="dataInicio">Data inicial do filtro (opcional).</param>
    /// <param name="dataFim">Data final do filtro (opcional).</param>
    /// <returns>Histórico de leituras do dispositivo.</returns>
    /// <response code="200">Histórico retornado com sucesso.</response>
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

    /// <summary>
    /// Retorna o status atual de um dispositivo IoT: última leitura, temperatura e conectividade.
    /// </summary>
    /// <param name="id">Identificador do dispositivo IoT.</param>
    /// <returns>Status atual do dispositivo.</returns>
    /// <response code="200">Status retornado com sucesso.</response>
    /// <response code="404">Dispositivo não encontrado.</response>
    [HttpGet("dispositivos/{id:long}/status")]
    [ProducesResponseType(typeof(DispositivoStatusDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetStatus(long id)
    {
        var result = await _dispositivoService.GetStatusAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Lista alertas de temperatura gerados automaticamente, com filtro opcional por status de resolução.
    /// </summary>
    /// <param name="resolvido">Filtrar por status: false = apenas não resolvidos (opcional).</param>
    /// <returns>Lista de alertas de temperatura.</returns>
    /// <response code="200">Alertas retornados com sucesso.</response>
    [HttpGet("alertas")]
    [ProducesResponseType(typeof(IEnumerable<AlertaTemperaturaResponseDto>), 200)]
    public async Task<IActionResult> GetAlertas([FromQuery] bool? resolvido)
    {
        var apenasNaoResolvidos = resolvido == false ? true : (bool?)null;
        var result = await _alertaService.GetAllAsync(apenasNaoResolvidos);
        return Ok(result);
    }
}
