namespace Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// Corpo de atualização de um item da tabela de preços (FD-09, ciclo FIN).
///
/// <para>
/// 🔴 <b>Sem <c>IdClinica</c>, pelo mesmo motivo de <see cref="ServicoPrecoCreateDto"/>:</b>
/// escopo de escrita vem do JWT. Sem <c>StAtiva</c> também — ativar/desativar tem verbo
/// próprio (<c>DELETE</c> e <c>POST /reativacao</c>), e aceitar o campo aqui daria dois
/// caminhos concorrentes para o mesmo estado.
/// </para>
///
/// <para>
/// ⚠️ <b>Alterar <c>VlPreco</c> NÃO altera nenhuma <c>COBRANCA</c> já lançada</b> —
/// <c>COBRANCA.VL_COBRADO</c> guarda uma cópia do valor no momento do lançamento (FD-08).
/// Corrigir um preço de tabela é decisão sobre o futuro, nunca reescrita do histórico
/// financeiro.
/// </para>
/// </summary>
public sealed class ServicoPrecoUpdateDto
{
    public string NmServico { get; init; } = string.Empty;

    public decimal VlPreco { get; init; }
}
