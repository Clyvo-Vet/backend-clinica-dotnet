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
/// 🔴 <b>FD-15 (ruling D-13) — AUTORIZAÇÃO MISTA, e por isso a política NÃO está mais no
/// controller.</b> Até a FD-15 o controller inteiro exigia <c>SomenteGestor</c>: o
/// veterinário LANÇAVA cobrança com <c>idServicoPreco</c> (<c>CobrancasController</c>,
/// <c>[Authorize]</c> simples) mas não conseguia LER a tabela de preços para descobrir qual
/// id mandar — podia escrever um id, não podia descobrir um. A ruling D-13 trata a tabela de
/// preços como <b>catálogo operacional</b>: qualquer usuário autenticado da clínica lê;
/// só o GESTOR decide preço.
/// <list type="bullet">
///   <item><description><b>Leitura</b> (<c>GET</c>, <c>GET/{id}</c>) →
///   <c>[Authorize]</c> simples: qualquer perfil autenticado da clínica, veterinário
///   incluído.</description></item>
///   <item><description><b>Escrita</b> (<c>POST</c>, <c>PUT</c>, <c>POST/{id}/reativacao</c>,
///   <c>DELETE</c>) → <c>[Authorize(Policy = SomenteGestor)]</c> no MÉTODO: preço continua
///   decisão comercial, só o gestor remarca a tabela.</description></item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>Por que a política teve de SAIR do controller — não é preferência de estilo, é
/// mecânica do framework.</b> <c>[Authorize]</c> de controller e de método <b>SE SOMAM</b>,
/// nunca se sobrepõem: com <c>[Authorize(Policy = SomenteGestor)]</c> ainda no controller, um
/// <c>[Authorize]</c> simples no <c>GET</c> <b>não afrouxaria nada</b> — a política do
/// controller continuaria exigindo GESTOR e o veterinário continuaria recebendo <c>403</c> no
/// catálogo que esta task existe para abrir. A única inversão que funciona é a de
/// <c>CobrancasController</c> (mesmo ciclo, autorização mista pelo mesmo motivo — ver o
/// doc-comment de lá): controller com <c>[Authorize]</c> simples, política nos métodos que a
/// ruling efetivamente restringe.
/// </para>
///
/// <para>
/// ⛔ <b><c>[AllowAnonymous]</c> NÃO é o desenho — seria regressão de segurança.</b> "Catálogo
/// operacional" significa "qualquer autenticado da clínica", nunca "público, sem token". Um
/// <c>GET</c> sem <c>[Authorize]</c> nenhum removeria a única barreira <b>declarada</b> de
/// autenticação desta rota.
/// <b>⚠️ Medido, para não superestimar o efeito e não subestimar o risco:</b> hoje a rota
/// <b>não</b> ficaria anônima na prática — <c>ClinicaContext.IdClinica</c> resolve
/// <c>clinicaId</c> com <c>GetRequiredClaimValue</c> e <b>lança</b> sem a claim, o que o
/// <c>ExceptionHandlerMiddleware</c> converte em <c>401</c> assim mesmo. O problema é que
/// isso é <b>acidente de implementação</b>, não garantia: passaria a depender de todo caminho
/// de leitura futuro tocar o contexto de clínica, e some no dia em que um endpoint não tocar.
/// A barreira tem que ser declarada.
/// </para>
///
/// <para>
/// <b>O que trava esta decisão contra regressão silenciosa não é o atributo, são os
/// testes.</b> Diferente do desenho anterior (controller inteiro protegido, onde "esqueci o
/// atributo" era o risco), aqui o risco inverso existe: alguém pode adicionar um método de
/// escrita novo e esquecer o <c>[Authorize(Policy = SomenteGestor)]</c> — o método nasceria
/// aberto a qualquer autenticado. <c>ServicosPrecoHttpTests</c> tranca os dois lados: os 2
/// <c>GET</c>s aceitam veterinário (<c>200</c>) com gestor como controle positivo, e cada
/// verbo de escrita continua barrando veterinário com <c>403</c>, também com gestor como
/// controle positivo.
/// </para>
///
/// <para>
/// 🔴 <b>Efeito colateral medido, não hipotético: o token PRÉ-FD-03 (sem a claim
/// <c>perfil</c>) agora LÊ.</b> <c>[Authorize]</c> simples exige só autenticação, não papel —
/// então um token desse formato, que <c>SomenteGestor</c> barraria com <c>403</c>, passa no
/// <c>GET</c> hoje. Continua barrado em toda escrita, onde a política ainda mora. Ver
/// <c>Token_pre_FD03_sem_a_claim_perfil_e_barrado_na_ESCRITA_com_403</c> — o teste antigo que
/// checava esse token contra o <c>GET</c> deixou de fazer sentido depois da FD-15 e foi
/// reapontado para um verbo de escrita, que é onde a política continua vivendo.
/// </para>
///
/// <para>
/// 🔴 <b>Nenhum endpoint aceita <c>IdClinica</c>.</b> Todo escopo — leitura e escrita — sai do
/// <c>clinicaId</c> do JWT dentro do service. Id de outra clínica devolve <c>404</c> em todos
/// os verbos, indistinguível de "não existe".
/// </para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/servicos-preco")]
public class ServicosPrecoController : ControllerBase
{
    private readonly IServicoPrecoService _service;

    public ServicosPrecoController(IServicoPrecoService service) => _service = service;

    /// <summary>
    /// Lista os serviços da tabela de preços da clínica do token. FD-16: com
    /// <c>incluirInativos=true</c> traz também os desativados (default: só ativos, igual ao
    /// comportamento anterior à FD-16).
    /// </summary>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="400">
    /// <c>incluirInativos</c> fora de <c>true</c>/<c>false</c> — o model binder de
    /// <c>bool</c> não-anulável recusa <c>1</c>, <c>0</c>, <c>on</c> e vazio com
    /// <c>400</c>. Medido na revisão G2 da FD-16, não inferido.
    /// </response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServicoPrecoResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInativos = false) =>
        Ok(await _service.ListarAsync(incluirInativos));

    /// <summary>Busca um serviço da clínica do token pelo id.</summary>
    /// <response code="200">Serviço encontrado (ativo ou desativado).</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica (indistinguível de propósito).</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ObterPorId(long id) => Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Cadastra um serviço na tabela de preços da clínica do token.</summary>
    /// <response code="201">Serviço criado.</response>
    /// <response code="400">Contrato inválido (nome vazio, preço negativo, mais de 2 decimais).</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    /// <response code="422">Já existe um serviço ATIVO com este nome nesta clínica.</response>
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [HttpPost]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
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
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">Nome em uso por outro serviço ATIVO, ou o serviço está desativado.</response>
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
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
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    /// <response code="422">O nome dele já está em uso por outro serviço ATIVO desta clínica.</response>
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [HttpPost("{id:long}/reativacao")]
    [ProducesResponseType(typeof(ServicoPrecoResponseDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Reativar(long id) => Ok(await _service.ReativarAsync(id));

    /// <summary>Desativa um serviço (soft delete — a linha permanece no banco).</summary>
    /// <response code="204">Serviço desativado.</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Desativar(long id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }
}
