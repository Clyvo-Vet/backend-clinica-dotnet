namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using FluentValidation.Results;
using Kura.Application.DTOs.Cobranca;
using Kura.Application.Validators;

/// <summary>
/// FD-10 — o contrato de entrada do lançamento de cobrança, e a razão de ele existir.
///
/// <para>
/// 🔴 <b>O Oracle tem <c>CHK_COBRANCA_VALOR CHECK (VL_COBRADO &gt;= 0)</c> e o <c>.NET</c>
/// não tinha nada equivalente.</b> Medido na FD-09 sobre a coluna irmã: sem esta regra, valor
/// negativo atravessa validator, service e EF sem objeção; o InMemory desta suíte
/// <b>grava</b> e devolve <c>201</c> (ele não aplica CHECK constraint nenhuma) e só produção
/// morre, como <c>ORA-02290</c> traduzido em <c>500</c>. O detector do banco não alcança este
/// caso no teste — este validator alcança.
/// </para>
///
/// <para>
/// 🔴 <b>E a faixa de <c>DtCobranca</c> fecha o modo de falha que NÃO é exceção nenhuma:</b>
/// <c>0001-01-01</c> não é nulo, passa pelo <c>NOT NULL</c> do Oracle e some de todo KPI por
/// período da FD-11 — receita lançada, gravada e invisível.
/// </para>
/// </summary>
public class CobrancaCreateValidatorTests
{
    private static readonly CobrancaCreateValidator Validator = new();

    private static ValidationResult Validar(
        decimal? valor = 100.00m,
        long? idServicoPreco = null,
        string? formaPagamento = null,
        DateTime? dtCobranca = null) =>
        Validator.Validate(new CobrancaCreateDto
        {
            VlCobrado = valor,
            IdServicoPreco = idServicoPreco,
            DsFormaPagamento = formaPagamento,
            DtCobranca = dtCobranca,
        });

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Valor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Valor_negativo_e_recusado()
    {
        var resultado = Validar(valor: -0.01m);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == CobrancaCreateValidator.MensagemValorNegativo);
    }

    [Fact]
    public void Valor_zero_e_ACEITO()
    {
        // 🔴 Controle positivo do teste acima, e regra de produto: cortesia é lançamento
        // legítimo. Um validator que recusasse zero passaria no teste do negativo e estaria
        // errado — a fronteira é `>= 0`, não `> 0`.
        Validar(valor: 0m).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valor_positivo_normal_e_aceito() => Validar(valor: 150.75m).IsValid.Should().BeTrue();

    [Fact]
    public void Valor_acima_do_maximo_de_NUMBER_10_2_e_recusado()
    {
        Validar(valor: CobrancaCreateValidator.ValorMaximo + 0.01m).IsValid.Should().BeFalse();

        // Controle positivo: o próprio máximo passa.
        Validar(valor: CobrancaCreateValidator.ValorMaximo).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valor_com_3_casas_decimais_e_recusado()
    {
        // O modo de falha medido na FD-07 é arredondamento SILENCIOSO do Oracle, não exceção:
        // 10,555 vira 10,56 e o gestor descobre pela fatura.
        Validar(valor: 10.555m).IsValid.Should().BeFalse();

        Validar(valor: 10.55m).IsValid.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Origem do valor (D-2)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Corpo_sem_valor_e_sem_servico_e_recusado()
    {
        var resultado = Validar(valor: null, idServicoPreco: null);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == CobrancaCreateValidator.MensagemSemOrigemDeValor);
    }

    [Fact]
    public void So_o_servico_basta_o_valor_e_copiado_no_service()
    {
        // 🔴 Este é o corpo MÍNIMO do princípio de desenho: {"idServicoPreco": N}. O
        // veterinário não digita valor no meio do atendimento.
        Validar(valor: null, idServicoPreco: 5).IsValid.Should().BeTrue();
    }

    [Fact]
    public void So_o_valor_basta_lancamento_avulso_e_legitimo() =>
        Validar(valor: 42m, idServicoPreco: null).IsValid.Should().BeTrue();

    [Fact]
    public void Os_dois_juntos_sao_aceitos_desconto_sobre_preco_de_tabela() =>
        Validar(valor: 42m, idServicoPreco: 5).IsValid.Should().BeTrue();

    [Fact]
    public void IdServicoPreco_nao_positivo_e_recusado() =>
        Validar(valor: null, idServicoPreco: 0).IsValid.Should().BeFalse();

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 DT_COBRANCA — o 0001-01-01 que some de todo KPI
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DtCobranca_0001_01_01_e_RECUSADA()
    {
        var resultado = Validar(dtCobranca: default(DateTime));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.ErrorMessage == CobrancaCreateValidator.MensagemDataForaDaFaixa);
    }

    [Fact]
    public void DtCobranca_ausente_e_aceita_o_service_usa_agora()
    {
        // Controle positivo: ausência é ausência, não erro. Se este teste falhasse, o
        // recusado acima estaria recusando o campo inteiro, não o valor degenerado.
        Validar(dtCobranca: null).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DtCobranca_de_ontem_e_aceita_fechamento_do_dia_anterior() =>
        Validar(dtCobranca: DateTime.UtcNow.AddDays(-1)).IsValid.Should().BeTrue();

    [Fact]
    public void DtCobranca_de_hoje_e_aceita() =>
        Validar(dtCobranca: DateTime.UtcNow).IsValid.Should().BeTrue();

    [Fact]
    public void DtCobranca_anterior_ao_piso_e_recusada() =>
        Validar(dtCobranca: CobrancaCreateValidator.DataMinima.AddDays(-1))
            .IsValid.Should().BeFalse();

    [Fact]
    public void DtCobranca_no_futuro_alem_da_tolerancia_e_recusada()
    {
        // Receita que ainda não aconteceu inflando o mês corrente da FD-11.
        Validar(dtCobranca: DateTime.UtcNow.AddDays(10)).IsValid.Should().BeFalse();

        // Controle positivo da tolerância: 1 hora à frente (relógio/fuso do cliente) passa.
        Validar(dtCobranca: DateTime.UtcNow.AddHours(1)).IsValid.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Forma de pagamento
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Forma_de_pagamento_acima_de_30_caracteres_e_recusada()
    {
        Validar(formaPagamento: new string('x', 31)).IsValid.Should().BeFalse();

        Validar(formaPagamento: new string('x', 30)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Forma_de_pagamento_ausente_e_aceita() =>
        Validar(formaPagamento: null).IsValid.Should().BeTrue();
}
