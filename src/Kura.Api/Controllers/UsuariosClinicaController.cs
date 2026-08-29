namespace Kura.Api.Controllers;

using Kura.Api.Extensions;
using Kura.Application.DTOs.UsuarioClinica;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// FD-04 — administração dos usuários (humanos) de uma clínica. É por aqui que o
/// <b>segundo</b> humano de uma clínica passa a existir: até esta task uma clínica nascia com
/// exatamente um usuário (o criado por <c>RegisterClinicaAsync</c> ou pela conversão da V17) e
/// não havia caminho de produto para acrescentar outro.
///
/// <para>
/// 🔴 <b><c>[Authorize(Policy = SomenteGestor)]</c> está no CONTROLLER, não em cada método</b>
/// — é o padrão que falha fechado sob manutenção: um endpoint novo acrescentado aqui nasce
/// protegido, e desproteger exige escrever <c>[AllowAnonymous]</c> de propósito. Marcar
/// método a método deixa a proteção dependente de alguém lembrar, e "esqueci o atributo" é
/// silencioso: o endpoint responde <c>200</c>.
/// </para>
///
/// <para>
/// <b>Como a política se comporta, medido em <c>UsuariosClinicaHttpTests</c>:</b> sem token
/// → <c>401</c> (desafio de autenticação); token válido de perfil <c>VETERINARIO</c> →
/// <c>403</c>; <b>token válido emitido ANTES da FD-03, sem a claim <c>perfil</c> → <c>403</c></b>
/// (a política é lista de PERMISSÃO: ausência de claim é negação); token de <c>GESTOR</c> →
/// <c>200</c>. Ver <c>AuthorizationExtensions</c> para por que a formulação inversa
/// ("não é veterinário, logo é gestor") teria dado <c>200</c> ao token antigo.
/// </para>
///
/// <para>
/// 🔴 <b>Nenhum endpoint aceita <c>IdClinica</c>.</b> Todo escopo — leitura e escrita — sai do
/// <c>clinicaId</c> do JWT dentro do service. Ver <c>UsuarioClinicaService</c>.
/// </para>
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
[ApiController]
[Route("api/v1/usuarios-clinica")]
public class UsuariosClinicaController : ControllerBase
{
    private readonly IUsuarioClinicaService _service;

    public UsuariosClinicaController(IUsuarioClinicaService service) => _service = service;

    /// <summary>Lista os usuários ativos da clínica do token.</summary>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioClinicaResponseDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    /// <summary>Busca um usuário da clínica do token pelo id.</summary>
    /// <response code="200">Usuário encontrado.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica (indistinguível de propósito).</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UsuarioClinicaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ObterPorId(long id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>
    /// Cria um usuário na clínica do token. A senha é gravada como hash BCrypt e nunca é
    /// devolvida.
    /// </summary>
    /// <response code="201">Usuário criado.</response>
    /// <response code="400">Contrato inválido (e-mail, senha curta, perfil desconhecido).</response>
    /// <response code="422">E-mail já usado nesta clínica, ou vínculo com veterinário de outra clínica.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioClinicaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Criar([FromBody] UsuarioClinicaCreateDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza e-mail, perfil e vínculo com veterinário.</summary>
    /// <response code="200">Usuário atualizado.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">E-mail em uso, vínculo inválido, ou a mudança deixaria a clínica sem gestor ativo.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(UsuarioClinicaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Atualizar(long id, [FromBody] UsuarioClinicaUpdateDto dto) =>
        Ok(await _service.AtualizarAsync(id, dto));

    /// <summary>
    /// Define a senha de um usuário da clínica. ⚠️ Não é recuperação de senha por
    /// autosserviço (fora de escopo na FD-04): é administração feita por quem já provou ser
    /// GESTOR daquela clínica.
    /// </summary>
    /// <response code="204">Senha definida.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    [HttpPut("{id:long}/senha")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DefinirSenha(
        long id, [FromBody] UsuarioClinicaSenhaUpdateDto dto)
    {
        await _service.DefinirSenhaAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um usuário (soft delete — a linha permanece no banco).</summary>
    /// <response code="204">Usuário desativado.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">Desativá-lo deixaria a clínica sem nenhum gestor ativo.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Desativar(long id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }
}
