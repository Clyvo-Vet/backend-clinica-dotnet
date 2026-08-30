namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using FluentValidation.Results;
using Kura.Application.DTOs.ServicoPreco;
using Kura.Application.Validators;

/// <summary>
/// FD-09 — o contrato de entrada do preço, e a razão de ele existir.
///
/// <para>
/// 🔴 <b>O Oracle tem <c>CHK_SERVICO_PRECO_VALOR CHECK (VL_PRECO &gt;= 0)</c> e o <c>.NET</c>
/// não tinha NADA equivalente</b> — achado da revisão G2 da FD-08. Sem estas regras um preço
/// negativo atravessa validator, service e EF sem objeção e morre no <c>INSERT</c>, como
/// <c>ORA-02290</c> traduzido em <c>500</c>. E o detector do banco <b>não alcança este caso na
/// suíte</b>: o provider InMemory não aplica CHECK constraint nenhuma, então a linha negativa
/// ficaria gravada e o teste passaria VERDE.
/// </para>
///
/// <para>
/// A paridade entre os dois validators é medida, não presumida: o <c>PUT</c> é a segunda porta
/// pela qual um preço já cadastrado viraria negativo.
/// </para>
/// </summary>
public class ServicoPrecoValidatorTests
{
    private static readonly ServicoPrecoCreateValidator Create = new();
    private static readonly ServicoPrecoUpdateValidator Update = new();

    private static ValidationResult ValidarCreate(decimal preco, string nome = "Consulta") =>
        Create.Validate(new ServicoPrecoCreateDto { NmServico = nome, VlPreco = preco });

    private static ValidationResult ValidarUpdate(decimal preco, string nome = "Consulta") =>
        Update.Validate(new ServicoPrecoUpdateDto { NmServico = nome, VlPreco = preco });

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-99999)]
    public void Preco_negativo_e_recusado_pelos_DOIS_validators(decimal preco)
    {
        var create = ValidarCreate(preco);
        var update = ValidarUpdate(preco);

        create.IsValid.Should().BeFalse();
        create.Errors.Should().Contain(e => e.PropertyName == nameof(ServicoPrecoCreateDto.VlPreco));
        update.IsValid.Should().BeFalse();
        update.Errors.Should().Contain(e => e.PropertyName == nameof(ServicoPrecoUpdateDto.VlPreco));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(10.55)]
    [InlineData(99999999.99)]
    public void Preco_valido_e_aceito_pelos_DOIS_validators(decimal preco)
    {
        // 🔴 CONTROLE POSITIVO, e ele é preciso em duas pontas. O CHECK do Oracle é `>= 0`,
        // não `> 0`: um validator escrito como GreaterThan(0) recusaria serviço gratuito
        // (retorno de consulta, cortesia) e passaria despercebido sem o caso do zero. E
        // 99999999,99 é o maior valor que cabe em NUMBER(10,2) — recusá-lo seria estreitar
        // a faixa por engano.
        ValidarCreate(preco).IsValid.Should().BeTrue();
        ValidarUpdate(preco).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Preco_acima_do_que_cabe_em_NUMBER_10_2_e_recusado()
    {
        // Sem esta regra o Oracle recusaria com ORA-01438 (value larger than specified
        // precision) — 500 em vez de 400.
        ValidarCreate(100_000_000.00m).IsValid.Should().BeFalse();
        ValidarUpdate(100_000_000.00m).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Preco_com_tres_casas_decimais_e_recusado()
    {
        // NUMBER(10,2) ARREDONDA em silêncio o que não cabe na escala — medido na FD-07:
        // 999.99 vira 1000 sem exceção quando a escala some. Recusar na borda é a única
        // forma de o número que entrou ser o número gravado.
        ValidarCreate(10.555m).IsValid.Should().BeFalse();
        ValidarUpdate(10.555m).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Zeros_a_direita_nao_contam_como_casas_decimais_extras()
    {
        // 10.5500m carrega escala 4 na representação decimal do CLR, mas vale exatamente
        // 10,55. `ignoreTrailingZeros: true` é o que impede um 400 absurdo para um valor
        // representável — e é essa a diferença entre os dois modos do PrecisionScale.
        ValidarCreate(10.5500m).IsValid.Should().BeTrue();
        ValidarUpdate(10.5500m).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_ou_so_de_espacos_e_recusado(string nome)
    {
        ValidarCreate(10.00m, nome).IsValid.Should().BeFalse();
        ValidarUpdate(10.00m, nome).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Nome_acima_de_200_caracteres_e_recusado_e_exatamente_200_e_aceito()
    {
        // NM_SERVICO VARCHAR2(200) na V18. O controle positivo em 200 existe porque um
        // MaximumLength(199) por engano passaria em qualquer teste que só medisse o excesso.
        ValidarCreate(10.00m, new string('a', 201)).IsValid.Should().BeFalse();
        ValidarUpdate(10.00m, new string('a', 201)).IsValid.Should().BeFalse();

        ValidarCreate(10.00m, new string('a', 200)).IsValid.Should().BeTrue();
        ValidarUpdate(10.00m, new string('a', 200)).IsValid.Should().BeTrue();
    }
}
