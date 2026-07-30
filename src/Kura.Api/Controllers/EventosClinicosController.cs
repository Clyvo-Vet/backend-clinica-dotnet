namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Documento;
using Kura.Application.DTOs.EventoClinico;
using Kura.Application.DTOs.Exame;
using Kura.Application.DTOs.Prescricao;
using Kura.Application.DTOs.Transcricao;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Registro e consulta de eventos clínicos: vacinas, prescrições, exames e consultas.
/// Cada POST de subtipo cria um EventoClinico + subtipo atomicamente.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/eventos-clinicos")]
public class EventosClinicosController : ControllerBase
{
    private readonly IEventoClinicoService _eventoService;
    private readonly IVacinaService _vacinaService;
    private readonly IPrescricaoService _prescricaoService;
    private readonly IExameService _exameService;
    private readonly IConsultaService _consultaService;
    private readonly ISoapDraftService _soapDraftService;
    private readonly IReceituarioPdfService _receituarioPdfService;

    public EventosClinicosController(
        IEventoClinicoService eventoService,
        IVacinaService vacinaService,
        IPrescricaoService prescricaoService,
        IExameService exameService,
        IConsultaService consultaService,
        ISoapDraftService soapDraftService,
        IReceituarioPdfService receituarioPdfService)
    {
        _eventoService = eventoService;
        _vacinaService = vacinaService;
        _prescricaoService = prescricaoService;
        _exameService = exameService;
        _consultaService = consultaService;
        _soapDraftService = soapDraftService;
        _receituarioPdfService = receituarioPdfService;
    }

    /// <summary>
    /// Lista eventos clínicos com filtros opcionais.
    /// </summary>
    /// <param name="petId">Filtrar por pet (opcional).</param>
    /// <param name="tipo">Tipo do evento: VACINA, PRESCRICAO, EXAME ou CONSULTA (opcional).</param>
    /// <param name="dataInicio">Data inicial do filtro por período (opcional).</param>
    /// <param name="dataFim">Data final do filtro por período (opcional).</param>
    /// <param name="veterinarioId">Filtrar por veterinário responsável (opcional).</param>
    /// <returns>Lista de eventos clínicos conforme filtros.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventoClinicoResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? petId,
        [FromQuery] string? tipo,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] long? veterinarioId)
    {
        var result = await _eventoService.GetByFiltersAsync(petId, tipo, dataInicio, dataFim, veterinarioId);
        return Ok(result);
    }

    /// <summary>
    /// Busca um evento clínico pelo ID.
    /// </summary>
    /// <param name="id">Identificador do evento clínico.</param>
    /// <returns>Dados do evento clínico encontrado.</returns>
    /// <response code="200">Evento encontrado.</response>
    /// <response code="404">Evento não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(EventoClinicoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _eventoService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Registra uma vacina (EventoClinico + Vacina em transação única).
    /// </summary>
    /// <param name="dto">Dados da vacina a ser registrada.</param>
    /// <returns>Vacina registrada com ID do evento clínico base.</returns>
    /// <response code="201">Vacina registrada com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet ou veterinário não encontrado.</response>
    [HttpPost("vacinas")]
    [ProducesResponseType(typeof(VacinaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> CreateVacina([FromBody] VacinaCreateDto dto)
    {
        var result = await _vacinaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }

    /// <summary>
    /// Registra uma prescrição medicamentosa (EventoClinico + Prescricao em transação única).
    /// </summary>
    /// <param name="dto">Dados da prescrição.</param>
    /// <returns>Prescrição registrada com ID do evento clínico base.</returns>
    /// <response code="201">Prescrição registrada com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet ou veterinário não encontrado.</response>
    [HttpPost("prescricoes")]
    [ProducesResponseType(typeof(PrescricaoResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> CreatePrescricao([FromBody] PrescricaoCreateDto dto)
    {
        var result = await _prescricaoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }

    /// <summary>
    /// Registra um exame clínico (EventoClinico + Exame em transação única).
    /// </summary>
    /// <param name="dto">Dados do exame.</param>
    /// <returns>Exame registrado com ID do evento clínico base.</returns>
    /// <response code="201">Exame registrado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet ou veterinário não encontrado.</response>
    [HttpPost("exames")]
    [ProducesResponseType(typeof(ExameResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> CreateExame([FromBody] ExameCreateDto dto)
    {
        var result = await _exameService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }

    /// <summary>
    /// Registra uma consulta clínica (EventoClinico + Consulta em transação única).
    /// </summary>
    /// <param name="dto">Dados da consulta.</param>
    /// <returns>Consulta registrada com ID do evento clínico base.</returns>
    /// <response code="201">Consulta registrada com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet ou veterinário não encontrado.</response>
    [HttpPost("consultas")]
    [ProducesResponseType(typeof(ConsultaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> CriarConsulta([FromBody] ConsultaCreateDto dto)
    {
        var result = await _consultaService.CriarConsultaAsync(dto);
        return CreatedAtAction(nameof(CriarConsulta), new { id = result.IdConsulta }, result);
    }

    /// <summary>
    /// Envia o áudio da consulta para transcrição (Whisper via Luna) e gera um draft SOAP.
    /// O resultado é salvo como rascunho não confirmado (ST_SOAP_CONFIRMADO='N') — o vet
    /// sempre revisa e confirma explicitamente via <see cref="ConfirmarSoap"/> antes de finalizar.
    /// Se a Luna estiver indisponível, transcrição/SOAP retornam nulos para edição manual.
    /// </summary>
    /// <param name="id">Identificador do evento clínico.</param>
    /// <param name="audio">Arquivo de áudio (mp3/m4a/wav).</param>
    /// <response code="200">Draft salvo (com ou sem sugestão da Luna).</response>
    /// <response code="404">Evento clínico não encontrado.</response>
    [HttpPost("{id:long}/transcricao")]
    [ProducesResponseType(typeof(EventoClinicoSoapResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> EnviarTranscricao(long id, IFormFile audio)
    {
        await using var stream = audio.OpenReadStream();
        var result = await _soapDraftService.EnviarTranscricaoAsync(
            id, stream, audio.FileName, audio.ContentType);
        return Ok(result);
    }

    /// <summary>
    /// Confirmação explícita do vet do texto SOAP (revisado/editado). Só então
    /// ST_SOAP_CONFIRMADO vira 'S' — nunca acontece automaticamente.
    /// </summary>
    /// <param name="id">Identificador do evento clínico.</param>
    /// <param name="dto">Texto SOAP final revisado pelo vet.</param>
    /// <response code="200">SOAP confirmado com sucesso.</response>
    /// <response code="404">Evento clínico não encontrado.</response>
    [HttpPut("{id:long}/soap")]
    [ProducesResponseType(typeof(EventoClinicoSoapResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ConfirmarSoap(long id, [FromBody] SoapConfirmarDto dto)
    {
        var result = await _soapDraftService.ConfirmarSoapAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Gera o PDF do receituário da prescrição (CRMV, pet, medicamento/posologia/duração
    /// e data), salva o arquivo em storage e persiste um Documento (path, nunca BLOB).
    /// </summary>
    /// <param name="id">Identificador do evento clínico (prescrição).</param>
    /// <response code="200">Receituário gerado com sucesso.</response>
    /// <response code="404">Evento clínico ou prescrição não encontrados.</response>
    [HttpPost("{id:long}/receituario")]
    [ProducesResponseType(typeof(DocumentoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GerarReceituario(long id)
    {
        var result = await _receituarioPdfService.GerarReceituarioAsync(id);
        return Ok(result);
    }
}
