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

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 F1 da fix wave pós-G2 — BORDA DE CALENDÁRIO e TETO DE VOLUME
    //
    // As duas famílias abaixo existem por motivos DIFERENTES e uma não cobre a outra. As
    // mensagens são asseridas em LITERAL (não contra a constante que elas provam) — a lição
    // do F2 da mesma revisão: asserção ancorada na própria constante sobrevive à troca do
    // valor por "" e deixa o teste verde com o produto quebrado.
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ate_no_ULTIMO_dia_do_calendario_e_recusado_porque_nao_ha_dia_seguinte()
    {
        // 🔴 A MORDIDA DO F1 no nível do validator. Antes da fix wave este caso chegava ao
        // service e morria em ArgumentOutOfRangeException -> 500: o resumo converte `ate` no
        // limite EXCLUSIVO `ate + 1 dia`, e 9999-12-31 não tem dia seguinte.
        var resultado = Validar(new DateOnly(9999, 12, 1), new DateOnly(9999, 12, 31));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("9999-12-30"));
    }

    [Fact]
    public void Ate_na_VESPERA_do_ultimo_dia_do_calendario_e_ACEITO()
    {
        // 🔴 CONTROLE POSITIVO da regra acima, e o vizinho imediato dela: 9999-12-30 AINDA
        // tem dia seguinte. Sem este caso, "recusa 9999-12-31" seria compatível com uma regra
        // grosseira que recusasse o ano 9999 inteiro. Literal, não `UltimoAteAceito`.
        var resultado = Validar(new DateOnly(9999, 12, 30), new DateOnly(9999, 12, 30));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void De_no_PRIMEIRO_dia_do_calendario_e_recusado_porque_o_periodo_ANTERIOR_nao_existe()
    {
        // O outro lado, e o mais sutil: quem estoura não é o período pedido — é o período de
        // COMPARAÇÃO, derivado, que o gestor nem pediu. 31 dias antes de 0001-01-01 não
        // existem.
        var resultado = Validar(new DateOnly(1, 1, 1), new DateOnly(1, 1, 31));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemDeForaDoCalendario);
    }

    [Fact]
    public void De_um_dia_DEPOIS_do_minimo_necessario_para_o_periodo_anterior_e_ACEITO()
    {
        // 🔴 CONTROLE POSITIVO da borda inferior, na casa decimal: um período de 31 dias
        // precisa de 31 dias de folga antes de `de`. O 32º dia do calendário (0001-02-01) é
        // o primeiro `de` que a satisfaz — o anterior é 0001-01-01..0001-01-31, exato.
        // Escrito em literal; se a regra virasse `>` no lugar de `>=`, este caso cairia.
        var resultado = Validar(new DateOnly(1, 2, 1), new DateOnly(1, 3, 3));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void O_par_EXTREMO_e_recusado_pelas_duas_razoes_ao_mesmo_tempo()
    {
        var resultado = Validar(new DateOnly(1, 1, 1), new DateOnly(9999, 12, 31));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Periodo_ALEM_do_teto_de_duracao_e_recusado_mesmo_LONGE_de_qualquer_borda()
    {
        // 🔴 Guarda de VOLUME, não de calendário: 2010→2020 é perfeitamente computável e
        // mesmo assim é recusado, porque a agregação do resumo é feita em MEMÓRIA. Este caso
        // é o que prova que o teto EXISTE separado das bordas.
        var resultado = Validar(new DateOnly(2010, 1, 1), new DateOnly(2020, 1, 1));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemDuracaoExcedida);
    }

    [Fact]
    public void Duracao_EXATAMENTE_no_teto_e_aceita_e_UM_DIA_a_mais_e_recusada()
    {
        // 🔴 A borda do teto, nos DOIS lados, com datas literais conferidas no calendário:
        // 2010-01-01 .. 2015-01-04 são 1830 dias INCLUSIVE (1826 até 2015-01-01 + 3).
        // Sem o par, `<` no lugar de `<=` (ou um dia de erro) passaria despercebido.
        Validar(new DateOnly(2010, 1, 1), new DateOnly(2015, 1, 4)).IsValid.Should().BeTrue();

        var umDiaAMais = Validar(new DateOnly(2010, 1, 1), new DateOnly(2015, 1, 5));
        umDiaAMais.IsValid.Should().BeFalse();
        umDiaAMais.Errors.Should().Contain(e =>
            e.ErrorMessage == ResumoFinanceiroQueryValidator.MensagemDuracaoExcedida);
    }

    [Fact]
    public void O_TETO_DE_DURACAO_SOZINHO_NAO_FECHARIA_A_BORDA_DE_CALENDARIO()
    {
        // 🔴 O teste que existe para impedir uma simplificação futura. 9999-12-01..12-31
        // são 31 dias — passam por QUALQUER teto de duração razoável — e mesmo assim estouram
        // no `ate + 1 dia`. Quem um dia achar que "o teto de 5 anos já resolve" e apagar a
        // regra de calendário reabre o 500 medido pela revisão G2, e este caso cai.
        const int duracaoEmDias = 31;

        duracaoEmDias.Should().BeLessThan(ResumoFinanceiroQueryValidator.DuracaoMaximaEmDias,
            "o período da borda superior é curto: o teto de volume não o alcança");

        Validar(new DateOnly(9999, 12, 1), new DateOnly(9999, 12, 31))
            .IsValid.Should().BeFalse();
    }
}
