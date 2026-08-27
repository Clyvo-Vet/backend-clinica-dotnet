namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Exame;
using Kura.Application.Validators;

public class ExameCreateValidatorTests
{
    private readonly ExameCreateValidator _sut = new();

    private static ExameCreateDto ValidDto(string dsObservacao = "Jejum de 12h antes da coleta.") => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtEvento = new DateTime(2026, 5, 6, 9, 0, 0),
        DsObservacao = dsObservacao,
        NmExame = "Hemograma completo",
        DsResultado = "Dentro dos parâmetros normais",
        DtRealizacao = new DateTime(2026, 5, 6, 9, 0, 0)
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        // Act
        var resultado = _sut.Validate(ValidDto());

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsObservacaoAusente_NaoRetornaErro()
    {
        // Arrange
        // DsObservacao é opcional (TASK-56) — o coalesce em ExameService satisfaz o
        // NOT NULL do Oracle (EVENTO_CLINICO.DS_OBSERVACAO).
        var dto = ValidDto(dsObservacao: string.Empty);

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsObservacaoMaiorQueColuna_RetornaErro()
    {
        // Arrange
        // EVENTO_CLINICO.DS_OBSERVACAO é VARCHAR2(1000) — payload maior estouraria
        // ORA-12899 no banco antes desta regra (re-review escopado, achado 3, FIX_4).
        var dto = ValidDto(dsObservacao: new string('a', 1001));

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ExameCreateDto.DsObservacao));
    }
}
