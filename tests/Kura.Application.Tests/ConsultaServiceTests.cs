namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class ConsultaServiceTests
{
    private readonly Mock<IRepository<Consulta>> _consultaRepoMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly ConsultaService _sut;

    public ConsultaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _sut = new ConsultaService(
            _consultaRepoMock.Object,
            _petRepoMock.Object,
            _vetRepoMock.Object,
            _uowMock.Object,
            _clinicaMock.Object);
    }

    private static ConsultaCreateDto ValidDto() => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtConsulta = new DateTime(2026, 5, 6, 9, 0, 0),
        DsMotivo = "Check-up anual",
        DsAnamnese = "Paciente apresenta letargia",
        DsDiagnostico = "Saudável"
    };

    [Fact]
    public async Task CriarConsultaAsync_DadosValidos_CriaDoisRegistrosAtomicamente()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        var result = await _sut.CriarConsultaAsync(ValidDto());

        result.Should().NotBeNull();
        result.IdPet.Should().Be(5);
        result.IdVeterinario.Should().Be(10);
        result.DsMotivo.Should().Be("Check-up anual");
        result.DsAnamnese.Should().Be("Paciente apresenta letargia");
        result.DsDiagnostico.Should().Be("Saudável");

        _consultaRepoMock.Verify(r => r.AddAsync(It.IsAny<Consulta>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CriarConsultaAsync_PetInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync((Pet?)null);

        var act = async () => await _sut.CriarConsultaAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*Pet*5*");
    }

    [Fact]
    public async Task CriarConsultaAsync_VeterinarioInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync((Veterinario?)null);

        var act = async () => await _sut.CriarConsultaAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*Veterinario*10*");
    }

    [Fact]
    public async Task CriarConsultaAsync_CommitChamadoUmaVez()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        await _sut.CriarConsultaAsync(ValidDto());

        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }
}
