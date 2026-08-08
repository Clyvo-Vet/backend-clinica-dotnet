namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Luna;
using Kura.Application.Validators;

public class TriageRequestValidatorTests
{
    private readonly TriageRequestValidator _sut = new();

    private static TriageRequestDto ValidDto(string dsUrgencia = "ALTA") => new()
    {
        IdInteracao = 100,
        IdTutor = 7,
        Sintomas = ["vomito"],
        DsUrgencia = dsUrgencia,
        NrScore = 80,
        DsRecomendacao = "Levar ao veterinário"
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        var resultado = _sut.Validate(ValidDto());

        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("BAIXA")]
    [InlineData("MEDIA")]
    [InlineData("ALTA")]
    public void Validate_DsUrgenciaValida_NaoRetornaErro(string urgencia)
    {
        var resultado = _sut.Validate(ValidDto(dsUrgencia: urgencia));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsUrgenciaForaDoEnum_RetornaErro()
    {
        var resultado = _sut.Validate(ValidDto(dsUrgencia: "CRITICA"));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.DsUrgencia));
    }

    [Fact]
    public void Validate_IdInteracaoZero_RetornaErro()
    {
        var dto = new TriageRequestDto
        {
            IdInteracao = 0,
            IdTutor = 7,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 80,
            DsRecomendacao = "Levar ao veterinário"
        };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.IdInteracao));
    }
}
