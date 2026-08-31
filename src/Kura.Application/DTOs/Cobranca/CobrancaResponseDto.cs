namespace Kura.Application.DTOs.Cobranca;

/// <summary>
/// Representação de leitura de um lançamento financeiro (FD-10, ciclo FIN).
///
/// <para>
/// <c>IdClinica</c> aparece aqui de propósito, apesar de não existir no DTO de escrita: no
/// caminho de LEITURA ele é a evidência observável de que a linha ficou no tenant do token —
/// é o que <c>CobrancasHttpTests</c> asserta.
/// </para>
///
/// <para>
/// ⚠️ <b>Sem agregação nenhuma aqui</b> — receita bruta, ticket médio e mix por serviço são
/// a FD-11. Este DTO é uma linha, não um relatório.
/// </para>
/// </summary>
public sealed class CobrancaResponseDto
{
    public long Id { get; init; }

    public long IdEventoClinico { get; init; }

    public long IdClinica { get; init; }

    /// <summary>Origem do lançamento, quando houve um item de catálogo. Ver o DTO de criação.</summary>
    public long? IdServicoPreco { get; init; }

    /// <summary>
    /// 🔴 <c>decimal</c>. A <b>cópia</b> gravada no momento do lançamento — não o preço
    /// atual do serviço. Ver <c>Cobranca.VlCobrado</c>.
    /// </summary>
    public decimal VlCobrado { get; init; }

    public string? DsFormaPagamento { get; init; }

    public DateTime DtCobranca { get; init; }

    public bool StAtiva { get; init; }

    public DateTime DtCriacao { get; init; }

    public DateTime? DtAtualizacao { get; init; }
}
