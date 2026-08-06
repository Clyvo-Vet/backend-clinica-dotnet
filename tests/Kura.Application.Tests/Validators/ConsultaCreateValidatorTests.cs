namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Validators;

public class ConsultaCreateValidatorTests
{
    private readonly ConsultaCreateValidator _sut = new();

    private static ConsultaCreateDto ValidDto(string dsObservacao = "Paciente calmo durante o exame.") => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtConsulta = new DateTime(2026, 5, 6, 9, 0, 0),
        DsMotivo = "Check-up anual",
        DsObservacao = dsObservacao
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        var resultado = _sut.Validate(ValidDto());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsObservacaoAusente_RetornaErro400EmVezDeEstourarNoBanco()
    {
        // TASK-47: EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL no Oracle. Antes dessa regra,
        // um POST sem DsObservacao passava pelo model binding (default "") e só era
        // barrado no INSERT com ORA-01400, vazando 500 cru em vez de 400 de validação.
        var dto = ValidDto(dsObservacao: string.Empty);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCreateDto.DsObservacao));
    }

    [Fact]
    public void Validate_DsObservacaoMaiorQueColuna_RetornaErro()
    {
        // Coluna é NVARCHAR2(1000) — payload maior estouraria no banco antes desta regra.
        var dto = ValidDto(dsObservacao: new string('a', 1001));

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCreateDto.DsObservacao));
    }
}
