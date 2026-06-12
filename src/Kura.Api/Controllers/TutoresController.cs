namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento de tutores (donos de pets). O cadastro via POST inicia o fluxo de onboarding com invite.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/tutores")]
public class TutoresController : ControllerBase
{
    private readonly ITutorService _service;

    public TutoresController(ITutorService service) => _service = service;

    /// <summary>
    /// Lista tutores com filtro textual opcional por nome ou CPF.
    /// </summary>
    /// <param name="busca">Texto parcial de nome ou CPF (opcional).</param>
    /// <returns>Lista de tutores ativos.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TutorResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? busca)
    {
        var result = await _service.SearchAsync(busca);
        return Ok(result);
    }

    /// <summary>
    /// Busca um tutor pelo ID.
    /// </summary>
    /// <param name="id">Identificador do tutor.</param>
    /// <returns>Dados do tutor encontrado.</returns>
    /// <response code="200">Tutor encontrado.</response>
    /// <response code="404">Tutor não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TutorResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Lista os pets vinculados a um tutor.
    /// </summary>
    /// <param name="id">Identificador do tutor.</param>
    /// <returns>Lista de pets do tutor.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="404">Tutor não encontrado.</response>
    [HttpGet("{id:long}/pets")]
    [ProducesResponseType(typeof(IEnumerable<PetResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetPets(long id)
    {
        var result = await _service.GetPetsAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra um novo tutor e gera automaticamente um invite de onboarding (token UUID, 7 dias, canal configurável).
    /// </summary>
    /// <param name="dto">Dados do tutor e canal de envio do invite (WHATSAPP | EMAIL | SMS).</param>
    /// <returns>Tutor criado com dados do invite gerado.</returns>
    /// <response code="201">Tutor e invite criados na mesma transação.</response>
    /// <response code="400">Dados inválidos ou canal inválido.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TutorComInviteResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] TutorCreateDto dto)
    {
        if (!long.TryParse(User.FindFirst("clinicaId")?.Value, out var clinicaId))
            return Unauthorized();

        var result = await _service.CreateAsync(dto, clinicaId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza os dados de um tutor existente.
    /// </summary>
    /// <param name="id">Identificador do tutor.</param>
    /// <param name="dto">Dados atualizados do tutor.</param>
    /// <returns>Tutor com dados atualizados.</returns>
    /// <response code="200">Tutor atualizado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Tutor não encontrado.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(TutorResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(long id, [FromBody] TutorUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Inativa um tutor (soft delete).
    /// </summary>
    /// <param name="id">Identificador do tutor.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Tutor inativado.</response>
    /// <response code="404">Tutor não encontrado.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.SoftDeleteAsync(id);
        return NoContent();
    }
}
