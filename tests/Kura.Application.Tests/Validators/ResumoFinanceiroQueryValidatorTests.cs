namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using FluentValidation.Results;
using Kura.Application.DTOs.Financeiro;
using Kura.Application.Validators;

/// <summary>
/// FD-11 — o contrato de entrada do resumo financeiro.
///
/// <para>
/// 🔴 <b>Por que a obrigatoriedade mora aqui e não num default de servidor.</b> A tentação é
/// "sem parâmetro, devolve o mês corrente"; ela produz o pior formato de bug possível para um
/// relatório — <c>200</c> com números <b>plausíveis do período errado</b>. Um <c>400</c> é
/// visível; um número plausível não é.
/// </para>
///
/// <para>
/// ⚠️ <b>O que este arquivo NÃO cobre, de propósito:</b> formato inválido
/// (<c>?de=ontem</c>) morre no <b>model binding</b>, antes deste validator existir na
/// história da requisição. Esse caso só é observável por HTTP, e está em
/// <c>FinanceiroResumoHttpTests</c> — testá-lo aqui seria testar a biblioteca errada.
/// </para>
/// </summary>
public class ResumoFinanceiroQueryValidatorTests
{
    private static readonly ResumoFinanceiroQueryValidator Validator = new();

    private static ValidationResult Validar(DateOnly? de, DateOnly? ate) =>
        Validator.Validate(new ResumoFinanceiroQueryDto { De = de, Ate = ate });

    [Fact]
    public void Periodo_completo_e_valido()
    {
        // Controle positivo: sem ele, um validator que recusasse tudo passaria em todos os
        // casos negativos abaixo.
        var resultado = Validar(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void De_igual_a_Ate_e_VALIDO_relatorio_de_um_dia()
    {
        // A regra é `de <= ate`, não `de < ate`: o fechamento diário é o caso mais comum.
        var resultado = Validar(new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 11));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void De_ausente_e_recusado()
    {
        var resultado = Validar(null, new DateOnly(2026, 5, 12));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemDeObrigatorio);
    }

    [Fact]
    public void Ate_ausente_e_recusado()
    {
        var resultado = Validar(new DateOnly(2026, 5, 10), null);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemAteObrigatorio);
    }

    [Fact]
    public void Os_DOIS_ausentes_sao_recusados_e_a_ausencia_de_um_nao_mascara_a_do_outro()
    {
        var resultado = Validar(null, null);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemDeObrigatorio);
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemAteObrigatorio);

        // 🔴 E a regra de intervalo NÃO dispara aqui: ela desreferenciaria os dois nulos.
        // Sem o `.When`, este caso seria NullReferenceException dentro do validator — ou
        // seja, 500 em vez de 400, no corpo mais trivialmente errado que existe.
        resultado.Errors.Should().NotContain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemIntervaloInvertido);
    }

    [Fact]
    public void Intervalo_INVERTIDO_e_recusado()
    {
        // Um intervalo invertido devolveria receita zero — indistinguível de um período sem
        // faturamento. É o mesmo defeito de "0 para dizer não medimos", pela porta da frente.
        var resultado = Validar(new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 10));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemIntervaloInvertido);
    }

    [Fact]
    public void Intervalo_invertido_por_UM_dia_tambem_e_recusado()
    {
        // Borda: um dia de inversão. Escrita em literal, não derivada.
        var resultado = Validar(new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 10));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemIntervaloInvertido);
    }
}
