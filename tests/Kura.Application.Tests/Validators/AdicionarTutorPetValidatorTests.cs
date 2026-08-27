namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Pet;
using Kura.Application.Validators;

public class AdicionarTutorPetValidatorTests
{
    private readonly AdicionarTutorPetValidator _sut = new();

    private static AdicionarTutorPetDto ValidDto(string dsVinculo = "CUIDADOR") => new()
    {
        IdTutor = 5,
        DsVinculo = dsVinculo
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
    public void Validate_DsVinculoAusente_NaoRetornaErro()
    {
        // Arrange
        // Sem NotEmpty(): AdicionarTutorPetDto.DsVinculo já tem default "CUIDADOR" e o
        // coalesce em PetService.AdicionarTutorAsync satisfaz o NOT NULL do Oracle (mesmo
        // padrão da TASK-60).
        var dto = ValidDto(dsVinculo: string.Empty);

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsVinculoMaiorQueColuna_RetornaErro()
    {
        // Arrange
        // TUTOR_PET.DS_VINCULO é VARCHAR2(40) NOT NULL
        // (backend-tutor-java/.../V1__initial_schema.sql:75) — payload maior estouraria
        // ORA-12899 no banco antes desta regra (achado 2 da revisão final do FIX_4).
        var dto = ValidDto(dsVinculo: new string('a', 41));

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AdicionarTutorPetDto.DsVinculo));
    }
}
