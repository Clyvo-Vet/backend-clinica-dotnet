namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class EventoClinicoServiceTests
{
    private readonly Mock<IEventoClinicoRepository> _eventoRepoMock = new();
    private readonly Mock<IRepository<Vacina>> _vacinaRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly VacinaService _sut;

    public EventoClinicoServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _sut = new VacinaService(
            _eventoRepoMock.Object, _vacinaRepoMock.Object,
            _uowMock.Object, _clinicaMock.Object, _petRepoMock.Object);
    }

    private VacinaCreateDto ValidDto() => new()
    {
        IdPet = 10L, IdVeterinario = 5L,
        DtEvento = new DateTime(2025, 1, 1),
        NmVacina = "Anti-rábica", NrLote = "L001", DsFabricante = "FabX"
    };

    private void SetupPet(long id) =>
        _petRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Pet { Id = id, NmPet = "Rex", IdClinica = 1 });

    [Fact]
    public async Task CreateAsync_PetValido_RetornaVacinaResponseDto()
    {
        SetupPet(10L);
        _vacinaRepoMock.Setup(r => r.AddAsync(It.IsAny<Vacina>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(ValidDto());

        result.Should().NotBeNull();
        result.IdPet.Should().Be(10L);
        result.NmVacina.Should().Be("Anti-rábica");
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_PetInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Pet?)null);
        var dto = new VacinaCreateDto
        {
            IdPet = 99L, IdVeterinario = 5L,
            DtEvento = new DateTime(2025, 1, 1),
            NmVacina = "Anti-rábica", NrLote = "L001", DsFabricante = "FabX"
        };

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CreateAsync_DtProximaDoseAnteriorAplicacao_LancaRegraDeNegocio()
    {
        SetupPet(10L);
        var dto = new VacinaCreateDto
        {
            IdPet = 10L, IdVeterinario = 5L,
            DtEvento = new DateTime(2025, 6, 1),
            DtProximaDose = new DateTime(2025, 5, 1),
            NmVacina = "Anti-rábica", NrLote = "L001", DsFabricante = "FabX"
        };

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task CreateAsync_SubtipoVacina_CommitChamadoUmaVez()
    {
        SetupPet(10L);
        _vacinaRepoMock.Setup(r => r.AddAsync(It.IsAny<Vacina>())).Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto());

        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }
}
