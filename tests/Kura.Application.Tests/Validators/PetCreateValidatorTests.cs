namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Pet;
using Kura.Application.Validators;

public class PetCreateValidatorTests
{
    private readonly PetCreateValidator _sut = new();

    private static PetCreateDto ValidDto(string dsVinculo = "PROPRIETARIO") => new()
    {
        IdEspecie = 1,
        IdRaca = 1,
        NmPet = "Rex",
        DtNascimento = new DateTime(2022, 1, 1),
        SgSexo = 'M',
        SgPorte = 'M',
        IdTutor = 5,
        StPrincipal = true,
        DsVinculo = dsVinculo
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        var resultado = _sut.Validate(ValidDto());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsVinculoAusente_NaoRetornaErro()
    {
        // Sem NotEmpty(): PetCreateDto.DsVinculo já tem default "PROPRIETARIO" e o
        // coalesce em PetService satisfaz o NOT NULL do Oracle (mesmo padrão da TASK-60).
        var dto = ValidDto(dsVinculo: string.Empty);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsVinculoMaiorQueColuna_RetornaErro()
    {
        // TUTOR_PET.DS_VINCULO é VARCHAR2(40) NOT NULL
        // (backend-tutor-java/.../V1__initial_schema.sql:75) — payload maior estouraria
        // ORA-12899 no banco antes desta regra (achado 2 da revisão final do FIX_4).
        var dto = ValidDto(dsVinculo: new string('a', 41));

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PetCreateDto.DsVinculo));
    }
}
