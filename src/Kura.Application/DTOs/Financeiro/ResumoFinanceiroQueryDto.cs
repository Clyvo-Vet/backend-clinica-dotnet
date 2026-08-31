namespace Kura.Application.DTOs.Financeiro;

/// <summary>
/// FD-11 — filtro de período de <c>GET /api/v1/financeiro/resumo</c>.
///
/// <para>
/// 🔴 <b>Não existe campo <c>IdClinica</c>, e isso é regra da trilha</b> (FD-09/FD-10): a
/// clínica sai do <c>clinicaId</c> do JWT, dentro do service. Um filtro de relatório que
/// aceitasse tenant no query string seria IDOR de leitura agregada — pior que o de leitura
/// de linha, porque devolve o faturamento inteiro do concorrente numa chamada.
/// </para>
///
/// <para>
/// 🔴 <b><c>DateOnly?</c>, não <c>DateTime</c>, e não <c>DateOnly</c> não-nullable.</b> Duas
/// decisões numa:
/// <list type="bullet">
///   <item><description><b>Data sem hora</b> — o gestor pede "de 01/08 até 31/08", nunca um
///   instante. Aceitar <c>DateTime</c> convidaria o cliente a mandar
///   <c>2026-08-31T00:00:00</c> e a perder o dia 31 inteiro sem nenhum erro visível: é
///   exatamente a armadilha de borda superior que o service resolve convertendo para
///   intervalo semiaberto.</description></item>
///   <item><description><b><c>Nullable</c></b> — a obrigatoriedade fica no
///   <c>ResumoFinanceiroQueryValidator</c>, ou seja, no código, e vira <c>400</c> com
///   mensagem própria. Com <c>DateOnly</c> não-nullable a ausência viraria
///   <c>0001-01-01</c> ou uma mensagem implícita do model binder, dependendo de trivia de
///   framework — a mesma classe de suposição que já mordeu este repo.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class ResumoFinanceiroQueryDto
{
    /// <summary>Primeiro dia do período, <b>inclusivo</b>. Obrigatório. Formato <c>YYYY-MM-DD</c>.</summary>
    public DateOnly? De { get; init; }

    /// <summary>
    /// Último dia do período, <b>inclusivo</b>. Obrigatório. Formato <c>YYYY-MM-DD</c>.
    ///
    /// <para>🔴 <b>Inclusivo é o contrato, e é o que o service tem de honrar convertendo
    /// para <c>[de 00:00, ate+1d 00:00)</c>.</b> Um filtro ingênuo
    /// <c>DtCobranca &lt;= ate</c> compara contra <c>ate 00:00</c> e descarta o último dia
    /// inteiro — receita real que some do relatório sem erro nenhum.</para>
    /// </summary>
    public DateOnly? Ate { get; init; }
}
