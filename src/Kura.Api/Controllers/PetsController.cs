namespace Kura.Api.Controllers;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento de pets, vínculos com tutores, timeline e carteira de vacinas.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pets")]
public class PetsController : ControllerBase
{
    private readonly IPetService _petService;
    private readonly IVacinaService _vacinaService;
    private readonly IEventoClinicoService _eventoService;

    public PetsController(
        IPetService petService,
        IVacinaService vacinaService,
        IEventoClinicoService eventoService)
    {
        _petService = petService;
        _vacinaService = vacinaService;
        _eventoService = eventoService;
    }

    /// <summary>
    /// Lista pets com filtros opcionais por tutor, espécie e porte.
    /// </summary>
    /// <param name="tutorId">Filtrar pelos pets de um tutor (opcional).</param>
    /// <param name="especieId">Filtrar por espécie (opcional).</param>
    /// <param name="porte">Filtrar por porte: P, M ou G (opcional).</param>
    /// <returns>Lista de pets ativos conforme os filtros.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PetResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? tutorId,
        [FromQuery] long? especieId,
        [FromQuery] char? porte)
    {
        var result = await _petService.GetByFiltersAsync(tutorId, especieId, porte);
        return Ok(result);
    }

    /// <summary>
    /// Busca um pet pelo ID.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Dados do pet encontrado.</returns>
    /// <response code="200">Pet encontrado.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _petService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra um novo pet vinculado a um tutor existente.
    /// </summary>
    /// <param name="dto">Dados do pet a ser cadastrado.</param>
    /// <returns>Pet criado com vínculo tutor-pet.</returns>
    /// <response code="201">Pet criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Tutor informado não encontrado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PetResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Create([FromBody] PetCreateDto dto)
    {
        var result = await _petService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza os dados de um pet.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <param name="dto">Dados atualizados do pet.</param>
    /// <returns>Pet com dados atualizados.</returns>
    /// <response code="200">Pet atualizado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(long id, [FromBody] PetUpdateDto dto)
    {
        var result = await _petService.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Inativa um pet (soft delete).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Pet inativado.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _petService.SoftDeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Vincula um tutor adicional a um pet (N:N tutor-pet).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <param name="dto">ID do tutor a vincular e indicador de tutor principal.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Tutor vinculado com sucesso.</response>
    /// <response code="400">Dados inválidos ou vínculo duplicado.</response>
    /// <response code="404">Pet ou tutor não encontrado.</response>
    [HttpPost("{id:long}/tutores")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> AdicionarTutor(long id, [FromBody] AdicionarTutorPetDto dto)
    {
        await _petService.AdicionarTutorAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Retorna a timeline cronológica de eventos clínicos do pet (vacinas, prescrições, exames, consultas).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Lista ordenada de eventos clínicos.</returns>
    /// <response code="200">Timeline retornada com sucesso.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}/timeline")]
    [ProducesResponseType(typeof(IEnumerable<TimelineItemDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetTimeline(long id)
    {
        var result = await _eventoService.GetTimelineAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Retorna as próximas vacinas agendadas para o pet.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Lista de vacinas com próximas doses pendentes.</returns>
    /// <response code="200">Vacinas retornadas.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}/proximas-vacinas")]
    [ProducesResponseType(typeof(IEnumerable<VacinaResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetProximasVacinas(long id)
    {
        var result = await _vacinaService.GetProximasVacinasAsync(id);
        return Ok(result);
    }
}
