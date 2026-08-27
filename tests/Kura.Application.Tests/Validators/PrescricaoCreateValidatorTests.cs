namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Prescricao;
using Kura.Application.Validators;

public class PrescricaoCreateValidatorTests
{
    private readonly PrescricaoCreateValidator _sut = new();

    private static PrescricaoCreateDto ValidDto(string dsObservacao = "Administrar após as refeições.") => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtEvento = new DateTime(2026, 5, 6, 9, 0, 0),
        DsObservacao = dsObservacao,
        IdMedicamento = 3,
        DsPosologia = "1 comprimido a cada 12 horas",
        NrDuracaoDias = 7
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
        // DsObservacao é opcional (TASK-56) — o coalesce no service satisfaz o NOT NULL do Oracle.
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
        // ORA-12899 no banco antes desta regra (achado 3 da revisão final do FIX_4).
        var dto = ValidDto(dsObservacao: new string('a', 1001));

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PrescricaoCreateDto.DsObservacao));
    }
}
