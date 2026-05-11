namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Auth;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Autenticação e registro de clínicas. Endpoints públicos — não requerem JWT.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;

    /// <summary>
    /// Autentica um veterinário e retorna um JWT.
    /// </summary>
    /// <param name="dto">Credenciais de acesso (e-mail e senha).</param>
    /// <returns>Token JWT com expiração e dados do veterinário autenticado.</returns>
    /// <response code="200">Login realizado com sucesso.</response>
    /// <response code="422">Credenciais inválidas ou regra de negócio violada.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _service.LoginAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra uma nova clínica veterinária e retorna as credenciais iniciais. Não requer autenticação.
    /// </summary>
    /// <param name="dto">Dados da clínica e do veterinário administrador.</param>
    /// <returns>Clínica criada com ID e credenciais do veterinário.</returns>
    /// <response code="201">Clínica registrada com sucesso.</response>
    /// <response code="422">Dados inválidos ou CNPJ já cadastrado.</response>
    [HttpPost("register-clinica")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterClinicaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> RegisterClinica([FromBody] RegisterClinicaDto dto)
    {
        var result = await _service.RegisterClinicaAsync(dto);
        return CreatedAtAction(nameof(RegisterClinica), new { id = result.IdClinica }, result);
    }
}
