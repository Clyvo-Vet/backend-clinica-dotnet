namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// FD-09 — contrato de entrada da criação de item da tabela de preços.
///
/// <para>
/// 🔴 <b>O piso de zero existe porque o Oracle tem <c>CHK_SERVICO_PRECO_VALOR CHECK
/// (VL_PRECO &gt;= 0)</c> e o <c>.NET</c> não tinha NADA equivalente</b> — achado da revisão
/// G2 da <c>FD-08</c>. Sem esta regra, <c>{"vlPreco": -1}</c> atravessa validator, service e
/// EF sem uma única objeção e só morre no <c>INSERT</c>, como <c>ORA-02290</c> traduzido em
/// <c>500</c> pelo <c>ExceptionHandlerMiddleware</c>. Pior: a suíte deste repositório roda em
/// InMemory, que <b>não aplica CHECK constraint nenhuma</b> — o preço negativo fica gravado e
/// o teste passa VERDE. O detector do banco não alcança este caso aqui; o validator alcança.
/// </para>
///
/// <para>
/// <b><c>PrecisionScale(10, 2)</c> espelha <c>NUMBER(10,2)</c>, e pelo mesmo tipo de motivo.</b>
/// O modo de falha do outro lado da faixa foi medido na FD-07: um valor que não cabe na
/// escala declarada é <b>arredondado em silêncio</b> pelo Oracle, não recusado — <c>10,555</c>
/// vira <c>10,56</c> sem aviso, e o gestor descobre pela fatura. Recusar com <c>400</c> na
/// borda é a única forma de o número que entrou ser o número que ficou gravado.
/// </para>
/// </summary>
public sealed class ServicoPrecoCreateValidator : AbstractValidator<ServicoPrecoCreateDto>
{
    /// <summary>
    /// Maior valor representável em <c>NUMBER(10,2)</c>: 8 dígitos inteiros + 2 decimais.
    /// Declarado como constante para que a mensagem de erro e a regra não possam divergir.
    /// </summary>
    public const decimal PrecoMaximo = 99_999_999.99m;

    public const string MensagemPrecoNegativo =
        "Preço não pode ser negativo (o banco recusa com CHECK VL_PRECO >= 0).";

    public ServicoPrecoCreateValidator()
    {
        RuleFor(x => x.NmServico)
            .NotEmpty().WithMessage("Nome do serviço é obrigatório.")
            .MaximumLength(200)
            .WithMessage("Nome do serviço deve ter no máximo 200 caracteres.");

        RuleFor(x => x.VlPreco)
            .GreaterThanOrEqualTo(0).WithMessage(MensagemPrecoNegativo)
            .LessThanOrEqualTo(PrecoMaximo)
            .WithMessage($"Preço deve ser no máximo {PrecoMaximo} (coluna NUMBER(10,2)).")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("Preço deve ter no máximo 2 casas decimais (coluna NUMBER(10,2)).");
    }
}
