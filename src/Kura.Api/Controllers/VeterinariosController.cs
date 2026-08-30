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
    /// Lista os veterinários da clínica autenticada.
    /// </summary>
    ///
    /// <remarks>
    /// 🔴 <b>FD-05 (fix wave pós-G2, R-2): o parâmetro de query <c>clinicaId</c> foi
    /// REMOVIDO.</b> Ele <b>parecia</b> escopar por clínica e não escopava nada: o query filter
    /// de tenant já restringe a consulta ao <c>clinicaId</c> do JWT, então o parâmetro só
    /// conseguia produzir dois resultados — a <b>própria</b> lista (quando o valor coincidia com
    /// o token) ou uma lista <b>vazia</b> (qualquer outro valor). Superfície que anuncia um poder
    /// que não tem é a mesma família de "UI para dado sem produtor" — e, num endpoint
    /// multi-tenant, ela convida exatamente a tentativa que a FD-05 fechou na escrita.
    ///
    /// <para>⚠️ <b>Compatível para trás:</b> cliente que ainda mande <c>?clinicaId=</c> não
    /// recebe erro — o ASP.NET ignora parâmetro de query não vinculado, do mesmo jeito que o
    /// <c>System.Text.Json</c> ignora o <c>idClinica</c> que sumiu do corpo do <c>POST</c>. Para
    /// quem mandava a própria clínica o efeito é idêntico; para quem mandava outra, a resposta
    /// deixa de ser uma lista vazia enganosa e passa a ser a lista correta do próprio tenant.</para>
    /// </remarks>
    ///
    /// <returns>Lista de veterinários da clínica do token.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VeterinarioResponseDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
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
