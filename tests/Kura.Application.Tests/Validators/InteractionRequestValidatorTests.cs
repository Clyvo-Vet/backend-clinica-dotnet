namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Luna;
using Kura.Application.Validators;

public class InteractionRequestValidatorTests
{
    private readonly InteractionRequestValidator _sut = new();

    private static InteractionRequestDto ValidDto(
        string dsCanal = "WHATSAPP",
        string dsDirecao = "INBOUND",
        string dsConteudo = "oi") => new()
    {
        IdTutor = 7,
        DsCanal = dsCanal,
        DsDirecao = dsDirecao,
        DsConteudo = dsConteudo,
        DtRecebimento = DateTime.UtcNow
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        // Act
        var resultado = _sut.Validate(ValidDto());

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("WHATSAPP")]
    [InlineData("EMAIL")]
    [InlineData("SMS")]
    public void Validate_DsCanalValido_NaoRetornaErro(string canal)
    {
        // Act
        var resultado = _sut.Validate(ValidDto(dsCanal: canal));

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsCanalForaDoEnum_RetornaErro()
    {
        // Act
        // DS_CANAL tem CHECK constraint no Oracle (CHK_INTERACAO_CANAL,
        // V15__interacao_canal.sql) — um valor fora da lista estouraria ORA-02290 (500)
        // sem essa validação.
        var resultado = _sut.Validate(ValidDto(dsCanal: "TELEGRAM"));

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(InteractionRequestDto.DsCanal));
    }

    [Theory]
    [InlineData("INBOUND")]
    [InlineData("OUTBOUND")]
    public void Validate_DsDirecaoValida_NaoRetornaErro(string direcao)
    {
        // Act
        var resultado = _sut.Validate(ValidDto(dsDirecao: direcao));

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsDirecaoForaDoEnum_RetornaErro()
    {
        // Act
        var resultado = _sut.Validate(ValidDto(dsDirecao: "LATERAL"));

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(InteractionRequestDto.DsDirecao));
    }

    [Fact]
    public void Validate_DsConteudoVazio_RetornaErro()
    {
        // Act
        // Diferente do padrão DsObservacao (TASK-56/60): ds_conteudo é obrigatório no
        // contrato Pydantic (ds_conteudo: str, sem default) — não é um campo
        // legitimamente opcional que precisa de coalesce no service.
        var resultado = _sut.Validate(ValidDto(dsConteudo: ""));

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(InteractionRequestDto.DsConteudo));
    }
}
