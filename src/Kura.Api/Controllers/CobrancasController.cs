namespace Kura.Api.Controllers;

using Kura.Api.Extensions;
using Kura.Application.DTOs.Cobranca;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// FD-10 — lançamento financeiro pendurado no atendimento (<c>COBRANCA</c>, criada pela V18 e
/// mapeada na FD-08). É por aqui que ela deixa de ser tabela sem caminho de produto.
///
/// <para>
/// 🔴 <b>A ROTA É SUBRECURSO DO EVENTO CLÍNICO, e isso é a decisão de desenho da task.</b>
/// <c>POST /api/v1/eventos-clinicos/{id}/cobrancas</c> — a cobrança nasce onde o atendimento
/// termina, no mesmo recurso que o veterinário já usa para fechar consulta, vacina, exame e
/// receituário. O princípio do ciclo é que <i>o dado do gestor nasce como subproduto do fluxo
/// do veterinário, nunca como trabalho extra para ele</i>; uma rota de primeiro nível
/// (<c>/api/v1/cobrancas</c>, com <c>idEventoClinico</c> no corpo) descreveria um formulário
/// financeiro separado, que é exatamente o desenho que o princípio proíbe.
/// </para>
///
/// <para>
/// 🔴 <b>AS DUAS AUTORIZAÇÕES SÃO DIFERENTES DE PROPÓSITO, e o motivo é medido.</b> A ruling
/// D-7 diz que o financeiro é <b>visível</b> só para o gestor. <c>EventosClinicosController</c>
/// é <c>[Authorize]</c> simples — quem cria evento clínico é o <b>veterinário</b>. Se a
/// ESCRITA de cobrança exigisse <c>SomenteGestor</c>, o veterinário não conseguiria registrar
/// a cobrança no fechamento do atendimento e o dado financeiro passaria a depender de o gestor
/// redigitar tudo depois. Então:
/// <list type="bullet">
///   <item><description><b>Escrita</b> (<c>POST</c>) → <c>[Authorize]</c>: veterinário e
///   gestor lançam. D-7 fala de visibilidade, não de quem produz o lançamento.</description></item>
///   <item><description><b>Leitura</b> (<c>GET</c>) → <c>[Authorize(Policy =
///   SomenteGestor)]</c>: é aqui que o financeiro fica visível, e é isso que a D-7
///   governa.</description></item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>Por que a política está NOS MÉTODOS DE LEITURA e não no controller — e por que a
/// inversão não funcionaria.</b> Os atributos <c>[Authorize]</c> de controller e de método
/// <b>se somam</b>, não se sobrepõem: com <c>[Authorize(Policy = SomenteGestor)]</c> no
/// controller, um <c>[Authorize]</c> simples no <c>POST</c> <b>não</b> afrouxa nada — a
/// política continuaria valendo e o veterinário levaria <c>403</c> ao lançar. Ou seja, o
/// padrão "política no controller" da FD-09/<c>UsuariosClinicaController</c> não é aplicável
/// a um controller de autorização mista; aqui ele produziria o defeito exato que a ruling
/// evita. O que trava a decisão contra regressão silenciosa não é o atributo, são os testes:
/// <c>CobrancasHttpTests</c> asserta veterinário → <c>201</c> no <c>POST</c> e veterinário →
/// <c>403</c> em cada <c>GET</c>, com o gestor como controle positivo dos dois lados.
/// </para>
///
/// <para>
/// 🔴 <b>Nenhum endpoint aceita <c>IdClinica</c>.</b> Todo escopo sai do <c>clinicaId</c> do
/// JWT dentro do service. Evento de outra clínica devolve <c>404</c>, indistinguível de "não
/// existe".
/// </para>
///
/// <para>
/// ⛔ <b>Sem agregação aqui</b> — receita bruta, ticket médio e mix por serviço são a FD-11.
/// E, por escopo negativo declarado (D-1/D-6): sem parcelamento, sem múltiplas formas na
/// mesma cobrança, <b>sem estorno</b>, sem gateway, sem status de processamento.
/// </para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/eventos-clinicos/{idEventoClinico:long}/cobrancas")]
public class CobrancasController : ControllerBase
{
    private readonly ICobrancaService _service;

    public CobrancasController(ICobrancaService service) => _service = service;

    /// <summary>
    /// Lança uma cobrança no atendimento. <b>Escrita: <c>[Authorize]</c> — o veterinário
    /// lança no fechamento do atendimento</b> (ver o cabeçalho da classe).
    ///
    /// <para>O corpo mínimo é <c>{"idServicoPreco": N}</c>: o valor é <b>copiado</b> do preço
    /// de tabela daquele instante, sem digitação. <c>vlCobrado</c> avulso (ou por cima do
    /// serviço, como desconto) também é lançamento legítimo (D-2).</para>
    /// </summary>
    /// <response code="201">Cobrança lançada.</response>
    /// <response code="400">Contrato inválido: sem origem de valor, valor negativo, mais de 2 decimais, data fora da faixa aceita.</response>
    /// <response code="401">Sem token, ou token inválido/expirado.</response>
    /// <response code="404">O evento clínico não existe, ou pertence a outra clínica (indistinguível de propósito).</response>
    /// <response code="422">O serviço de preço informado não é desta clínica, ou está desativado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CobrancaResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Lancar(
        long idEventoClinico, [FromBody] CobrancaCreateDto dto)
    {
        var lancada = await _service.LancarAsync(idEventoClinico, dto);

        return CreatedAtAction(
            nameof(ObterPorId),
            new { idEventoClinico, id = lancada.Id },
            lancada);
    }

    /// <summary>
    /// Cobranças lançadas no atendimento. <b>Leitura: <c>SomenteGestor</c> (D-7).</b>
    /// </summary>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token sem a claim).</response>
    /// <response code="404">O evento clínico não existe, ou pertence a outra clínica.</response>
    [HttpGet]
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [ProducesResponseType(typeof(IEnumerable<CobrancaResponseDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Listar(long idEventoClinico) =>
        Ok(await _service.ListarDoEventoAsync(idEventoClinico));

    /// <summary>
    /// Uma cobrança pelo id. <b>Leitura: <c>SomenteGestor</c> (D-7).</b>
    ///
    /// <para>⚠️ O <c>VlCobrado</c> devolvido é a <b>cópia</b> gravada no lançamento, nunca o
    /// preço atual do serviço de origem — remarcar a tabela de preços não reescreve o
    /// histórico financeiro.</para>
    /// </summary>
    /// <response code="200">Cobrança encontrada.</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR.</response>
    /// <response code="404">Não existe, ou pertence a outra clínica.</response>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [ProducesResponseType(typeof(CobrancaResponseDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> ObterPorId(long idEventoClinico, long id) =>
        Ok(await _service.ObterPorIdAsync(id));
}
