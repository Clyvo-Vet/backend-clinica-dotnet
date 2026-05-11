namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Catálogo de medicamentos da clínica. Usado para autocomplete em prescrições.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/medicamentos")]
public class MedicamentosController : ControllerBase
{
    private readonly IMedicamentoService _service;

    public MedicamentosController(IMedicamentoService service) => _service = service;

    /// <summary>
    /// Lista medicamentos paginados. Use o parâmetro busca para autocomplete por nome ou princípio ativo.
    /// </summary>
    /// <param name="busca">Texto de busca parcial por nome ou princípio ativo (opcional).</param>
    /// <param name="page">Número da página (padrão: 1).</param>
    /// <param name="pageSize">Itens por página (padrão: 20, máx: 100).</param>
    /// <returns>Página de medicamentos com metadados de paginação.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<MedicamentoResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? busca = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.ListarAsync(busca, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Busca um medicamento pelo ID.
    /// </summary>
    /// <param name="id">Identificador do medicamento.</param>
    /// <returns>Dados do medicamento encontrado.</returns>
    /// <response code="200">Medicamento encontrado.</response>
    /// <response code="404">Medicamento não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra um novo medicamento no catálogo da clínica.
    /// </summary>
    /// <param name="dto">Dados do medicamento a ser cadastrado.</param>
    /// <returns>Medicamento criado.</returns>
    /// <response code="201">Medicamento criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] MedicamentoCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
