namespace Kura.Application.DTOs.Cobranca;

/// <summary>
/// Corpo do lançamento de cobrança num evento clínico (FD-10, ciclo FIN).
///
/// <para>
/// 🔴 <b>NÃO existe campo <c>IdEventoClinico</c> nem <c>IdClinica</c>, e isso é a regra da
/// task.</b> O evento vem da ROTA (<c>POST /api/v1/eventos-clinicos/{id}/cobrancas</c>) e a
/// clínica vem do <c>clinicaId</c> do JWT, dentro de <c>CobrancaService</c>. É o padrão da
/// FD-09 e a correção da FD-05, onde <c>VeterinarioService.CreateAsync</c> grava
/// <c>dto.IdClinica</c> sem comparar com o token — aqui o campo <b>não existe</b>.
/// </para>
///
/// <para>
/// <b>D-2 — as duas origens de valor.</b> O lançamento pode apontar um item da tabela de
/// preços (<see cref="IdServicoPreco"/>) ou trazer o valor digitado
/// (<see cref="VlCobrado"/>). Pelo menos um dos dois é obrigatório; o validator recusa o
/// corpo que não traz nenhum.
/// </para>
/// </summary>
public sealed class CobrancaCreateDto
{
    /// <summary>
    /// Item da tabela de preços que originou o lançamento, quando houve um.
    /// <b>Opcional</b> — valor avulso é lançamento legítimo (D-2).
    ///
    /// <para>
    /// 🔴 <b>É rastreabilidade de ORIGEM, nunca fonte de valor em leitura.</b> Quando
    /// informado sem <see cref="VlCobrado"/>, o service <b>COPIA</b> o <c>VL_PRECO</c>
    /// daquele instante para <c>VL_COBRADO</c>. Depois disso a cobrança não olha mais para
    /// o serviço: remarcar a tabela de preços não reescreve histórico financeiro.
    /// </para>
    /// </summary>
    public long? IdServicoPreco { get; init; }

    /// <summary>
    /// Valor cobrado, quando digitado direto (ou quando difere do preço de tabela — um
    /// desconto concedido no balcão é lançamento legítimo).
    ///
    /// <para><b>Nullable de propósito:</b> <c>decimal</c> é struct, então "não informado"
    /// e "informado como zero" seriam indistinguíveis num tipo não-nullable — e zero é um
    /// valor legítimo (cortesia). Com <c>decimal?</c>, ausência é ausência: o service cai
    /// na cópia do preço de tabela, e o validator recusa o corpo que não traz nem valor nem
    /// serviço.</para>
    ///
    /// <para>🔴 <c>decimal</c>, nunca <c>double</c> — ver <c>Cobranca.VlCobrado</c>. O piso
    /// de zero está em <c>CobrancaCreateValidator</c>: o Oracle tem
    /// <c>CHK_COBRANCA_VALOR CHECK (VL_COBRADO &gt;= 0)</c> e o InMemory da suíte não aplica
    /// CHECK nenhuma — sem o validator, valor negativo fica gravado, o teste passa verde e
    /// produção devolve <c>ORA-02290</c>/<c>500</c>.</para>
    /// </summary>
    public decimal? VlCobrado { get; init; }

    /// <summary>
    /// Meio de pagamento declarado pelo cliente (<c>VARCHAR2(30)</c>, nullable na V18).
    /// <b>Não é status de processamento</b> (D-1): esta cobrança não é conciliada com
    /// gateway nenhum.
    /// </summary>
    public string? DsFormaPagamento { get; init; }

    /// <summary>
    /// Data do lançamento. <b>Opcional</b> — ausente, o service usa
    /// <c>DateTime.UtcNow</c>.
    ///
    /// <para>Aceitar data informada existe por um caso real: o fechamento do dia anterior
    /// lançado na manhã seguinte. O que o validator recusa é o que corrompe os KPI da
    /// FD-11 — data anterior a <c>2000-01-01</c> (que captura
    /// <c>0001-01-01</c>, o <c>default(DateTime)</c> que passa pelo <c>NOT NULL</c> do
    /// Oracle e some de todo relatório por período) e data no futuro além de um dia de
    /// tolerância (receita que ainda não existe inflando o mês corrente).</para>
    ///
    /// <para>⚠️ <b>Limite declarado para a FD-11 (F2 da revisão G2):</b> a tolerância futura
    /// de 1 dia <b>atravessa fronteira de mês</b> — 31/01 no limite cai em 01/02, no balde do
    /// mês seguinte. A decisão de manter a folga, e o porquê, estão em
    /// <c>CobrancaCreateValidator.ToleranciaFutura</c>.</para>
    /// </summary>
    public DateTime? DtCobranca { get; init; }
}
