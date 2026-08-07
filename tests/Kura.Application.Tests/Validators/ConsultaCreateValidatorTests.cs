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
    public void Validate_DsObservacaoAusente_NaoRetornaErro()
    {
        // TASK-56: reverte parcialmente a TASK-47, de propósito. A regra NotEmpty() daquela
        // task transformou uma restrição de armazenamento (NOT NULL no Oracle) em regra de
        // negócio — mas o form SOAP do app (consulta/[idPet].tsx) exige apenas um dos quatro
        // campos S/O/A/P preenchido, então "Plano" (DsObservacao) vazio é um caso legítimo e
        // não pode devolver 400. Quem satisfaz o NOT NULL do Oracle agora é o coalesce em
        // ConsultaService (sentinela "Sem observações"), não este validator.
        var dto = ValidDto(dsObservacao: string.Empty);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
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
