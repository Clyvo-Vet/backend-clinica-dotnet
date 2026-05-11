namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento de veterinários da clínica.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/veterinarios")]
public class VeterinariosController : ControllerBase
{
    private readonly IVeterinarioService _service;

    public VeterinariosController(IVeterinarioService service) => _service = service;

    /// <summary>
    /// Lista todos os veterinários, com filtro opcional por clínica.
    /// </summary>
    /// <param name="clinicaId">Filtro por ID da clínica (opcional).</param>
    /// <returns>Lista de veterinários.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VeterinarioResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] long? clinicaId)
    {
        var result = clinicaId.HasValue
            ? await _service.GetByClinicaAsync(clinicaId.Value)
            : await _service.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Busca um veterinário pelo ID.
    /// </summary>
    /// <param name="id">Identificador do veterinário.</param>
    /// <returns>Dados do veterinário encontrado.</returns>
    /// <response code="200">Veterinário encontrado.</response>
    /// <response code="404">Veterinário não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra um novo veterinário na clínica.
    /// </summary>
    /// <param name="dto">Dados do veterinário a ser cadastrado.</param>
    /// <returns>Veterinário criado.</returns>
    /// <response code="201">Veterinário criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] VeterinarioCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza os dados de um veterinário.
    /// </summary>
    /// <param name="id">Identificador do veterinário.</param>
    /// <param name="dto">Dados atualizados.</param>
    /// <returns>Veterinário com dados atualizados.</returns>
    /// <response code="200">Veterinário atualizado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Veterinário não encontrado.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(long id, [FromBody] VeterinarioUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Inativa um veterinário (soft delete).
    /// </summary>
    /// <param name="id">Identificador do veterinário.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Veterinário inativado.</response>
    /// <response code="404">Veterinário não encontrado.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.SoftDeleteAsync(id);
        return NoContent();
    }
}
