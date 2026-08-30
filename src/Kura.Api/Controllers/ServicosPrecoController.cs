namespace Kura.Api.Controllers;

using Kura.Api.Extensions;
using Kura.Application.DTOs.ServicoPreco;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// FD-09 — tabela de preços da clínica (<c>SERVICO_PRECO</c>, criada pela V18 e mapeada na
/// FD-08). É por aqui que ela deixa de ser tabela sem caminho de produto.
///
/// <para>
/// 🔴 <b><c>[Authorize(Policy = SomenteGestor)]</c> está no CONTROLLER, não em cada método</b>
/// — mesmo padrão de <c>UsuariosClinicaController</c>, e pelo mesmo motivo: um endpoint novo
/// acrescentado aqui nasce protegido, e desproteger exige escrever <c>[AllowAnonymous]</c> de
/// propósito. Marcar método a método deixa a proteção dependente de alguém lembrar, e
/// "esqueci o atributo" é silencioso — o endpoint responde <c>200</c>. Preço é decisão
/// comercial: um veterinário não remarca a tabela da clínica.
/// </para>
///
/// <para>
/// <b>Como a política se comporta, medido em <c>ServicosPrecoHttpTests</c>:</b> sem token →
/// <c>401</c>; token válido de perfil <c>VETERINARIO</c> → <c>403</c>; <b>token válido emitido
/// ANTES da FD-03, sem a claim <c>perfil</c> → <c>403</c></b> (a política é lista de
/// PERMISSÃO: ausência de claim é negação, e tokens desse formato continuam válidos até
/// expirar); token de <c>GESTOR</c> → <c>200</c>.
/// </para>
///
/// <para>
/// 🔴 <b>Nenhum endpoint aceita <c>IdClinica</c>.</b> Todo escopo — leitura e escrita — sai do
/// <c>clinicaId</c> do JWT dentro do service. Id de outra clínica devolve <c>404</c> em todos
/// os verbos, indistinguível de "não existe".
/// </para>
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
[ApiController]
[Route("api/v1/servicos-preco")]
public class ServicosPrecoController : ControllerBase
{
    private readonly IServicoPrecoService _service;

    public ServicosPrecoController(IServicoPrecoService service) => _service = service;

    /// <summary>Lista os serviços ativos da tabela de preços da clínica do token.</summary>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServicoPrecoResponseDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    /// <summary>Busca um serviço da clínica do token pelo id.</summary>
    /// <response code="200">Serviço encontrado (ativo ou desativado).</response>
    /// <response code="404">Não existe, ou pertence a outra clínica (indistinguível de propósito).</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ObterPorId(long id) => Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Cadastra um serviço na tabela de preços da clínica do token.</summary>
    /// <response code="201">Serviço criado.</response>
    /// <response code="400">Contrato inválido (nome vazio, preço negativo, mais de 2 decimais).</response>
    /// <response code="422">Já existe um serviço ATIVO com este nome nesta clínica.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Criar([FromBody] ServicoPrecoCreateDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>
    /// Atualiza nome e preço. ⚠️ <b>Não altera nenhuma cobrança já lançada</b> —
    /// <c>COBRANCA.VL_COBRADO</c> guarda a cópia do valor do momento do lançamento (FD-08).
    /// </summary>
    /// <response code="200">Serviço atualizado.</response>
    /// <response code="400">Contrato inválido.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">Nome em uso por outro serviço ATIVO, ou o serviço está desativado.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Atualizar(long id, [FromBody] ServicoPrecoUpdateDto dto) =>
        Ok(await _service.AtualizarAsync(id, dto));

    /// <summary>
    /// Reativa um serviço desativado desta clínica. Existe para que desativar não seja porta
    /// de mão única para o <b>id</b> — recadastrar resolveria o nome, mas criaria linha nova,
    /// e <c>COBRANCA.ID_SERVICO_PRECO</c> aponta para o id.
    /// </summary>
    /// <response code="200">Serviço ativo (reativado agora, ou já ativo — a operação é idempotente).</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">O nome dele já está em uso por outro serviço ATIVO desta clínica.</response>
    [HttpPost("{id:long}/reativacao")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Reativar(long id) => Ok(await _service.ReativarAsync(id));

    /// <summary>Desativa um serviço (soft delete — a linha permanece no banco).</summary>
    /// <response code="204">Serviço desativado.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Desativar(long id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }
}
