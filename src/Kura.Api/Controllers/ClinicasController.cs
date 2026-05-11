namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Clinica;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento da clínica autenticada.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/clinicas")]
public class ClinicasController : ControllerBase
{
    private readonly IClinicaService _service;

    public ClinicasController(IClinicaService service) => _service = service;

    /// <summary>
    /// Retorna os dados de uma clínica pelo ID.
    /// </summary>
    /// <param name="id">Identificador da clínica.</param>
    /// <returns>Dados da clínica encontrada.</returns>
    /// <response code="200">Clínica encontrada.</response>
    /// <response code="404">Clínica não encontrada.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ClinicaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza os dados da clínica.
    /// </summary>
    /// <param name="id">Identificador da clínica.</param>
    /// <param name="dto">Dados atualizados da clínica.</param>
    /// <returns>Clínica com dados atualizados.</returns>
    /// <response code="200">Clínica atualizada.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Clínica não encontrada.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ClinicaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(long id, [FromBody] ClinicaUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Inativa a clínica (soft delete — dados preservados no banco).
    /// </summary>
    /// <param name="id">Identificador da clínica.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Clínica inativada.</response>
    /// <response code="404">Clínica não encontrada.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.SoftDeleteAsync(id);
        return NoContent();
    }
}
