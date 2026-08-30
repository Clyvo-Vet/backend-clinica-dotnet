namespace Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// Representação de leitura de um item da tabela de preços (FD-09, ciclo FIN).
///
/// <para>
/// <c>IdClinica</c> aparece aqui de propósito, apesar de não existir nos DTOs de escrita: no
/// caminho de LEITURA ele é a evidência observável de que a linha ficou no tenant do token —
/// é o que <c>ServicosPrecoHttpTests</c> asserta ao mandar um <c>idClinica</c> alheio no
/// corpo e conferir que a resposta traz o do JWT.
/// </para>
/// </summary>
public sealed class ServicoPrecoResponseDto
{
    public long Id { get; init; }

    public long IdClinica { get; init; }

    public string NmServico { get; init; } = string.Empty;

    /// <summary>
    /// 🔴 <c>decimal</c>. Trocar por <c>double</c> aqui reintroduziria, na borda HTTP, o erro
    /// que <c>ServicoPreco.VlPreco</c> evita no domínio.
    /// </summary>
    public decimal VlPreco { get; init; }

    public bool StAtiva { get; init; }

    public DateTime DtCriacao { get; init; }

    public DateTime? DtAtualizacao { get; init; }
}
