namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/medicamentos")]
public class MedicamentosController : ControllerBase
{
    private readonly IMedicamentoService _service;

    public MedicamentosController(IMedicamentoService service) => _service = service;

    /// <summary>
    /// Lista medicamentos paginados. Use ?busca= para autocomplete.
    /// </summary>
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

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] MedicamentoCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
