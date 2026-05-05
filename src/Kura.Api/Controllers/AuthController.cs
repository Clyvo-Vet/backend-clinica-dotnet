namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Auth;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _service.LoginAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Cadastro público de nova clínica. Não requer autenticação.
    /// </summary>
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
