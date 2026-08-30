namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// FD-09 — contrato de entrada da atualização de item da tabela de preços.
///
/// <para>
/// As regras são as MESMAS de <see cref="ServicoPrecoCreateValidator"/>, e estão repetidas de
/// propósito em vez de herdadas: o <c>PUT</c> é o caminho pelo qual um preço já cadastrado
/// vira negativo, e um validator de update mais frouxo que o de create deixa entrar pela
/// segunda porta o que a primeira fecha. <c>ServicoPrecoValidatorParidadeTests</c> trava a
/// paridade — as duas regras de preço são conferidas lado a lado com a mesma entrada.
/// </para>
/// </summary>
public sealed class ServicoPrecoUpdateValidator : AbstractValidator<ServicoPrecoUpdateDto>
{
    public ServicoPrecoUpdateValidator()
    {
        RuleFor(x => x.NmServico)
            .NotEmpty().WithMessage("Nome do serviço é obrigatório.")
            .MaximumLength(200)
            .WithMessage("Nome do serviço deve ter no máximo 200 caracteres.");

        RuleFor(x => x.VlPreco)
            .GreaterThanOrEqualTo(0)
            .WithMessage(ServicoPrecoCreateValidator.MensagemPrecoNegativo)
            .LessThanOrEqualTo(ServicoPrecoCreateValidator.PrecoMaximo)
            .WithMessage(
                $"Preço deve ser no máximo {ServicoPrecoCreateValidator.PrecoMaximo} "
                + "(coluna NUMBER(10,2)).")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("Preço deve ter no máximo 2 casas decimais (coluna NUMBER(10,2)).");
    }
}
