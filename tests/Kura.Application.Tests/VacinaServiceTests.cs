namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class VacinaServiceTests
{
    // Ids da seed real (afterMigrate__seeds_dev.sql): CONSULTA=1, TELEORIENTACAO=2, VACINA=3,
    // PRESCRICAO=4, EXAME=5. O antigo const hardcoded usava IdTipoEventoVacina=1, que colidia com
    // CONSULTA — este teste garante que a resolução por CD_TIPO usa o id correto e distinto.
    private const long IdTipoEventoVacinaSeed = 3L;

    private readonly Mock<IEventoClinicoRepository> _eventoRepoMock = new();
    private readonly Mock<IRepository<Vacina>> _vacinaRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<ITipoEventoService> _tipoEventoServiceMock = new();
    private readonly VacinaService _sut;

    public VacinaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("VACINA"))
            .ReturnsAsync(IdTipoEventoVacinaSeed);
        _sut = new VacinaService(
            _eventoRepoMock.Object,
            _vacinaRepoMock.Object,
            _uowMock.Object,
            _clinicaMock.Object,
            _petRepoMock.Object,
            _tipoEventoServiceMock.Object);
    }

    private static VacinaCreateDto ValidDto() => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtEvento = new DateTime(2026, 5, 6, 9, 0, 0),
        NmVacina = "V10",
        NrLote = "L123",
        DsFabricante = "Zoetis"
    };

    [Fact]
    public async Task CreateAsync_ResolveIdTipoEventoPorCdTipo_PersisteFkDistintaDeOutrosTipos()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        Vacina? vacinaAdicionada = null;
        _vacinaRepoMock.Setup(r => r.AddAsync(It.IsAny<Vacina>()))
            .Callback<Vacina>(v => vacinaAdicionada = v)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto());

        _tipoEventoServiceMock.Verify(t => t.GetIdByCdTipoAsync("VACINA"), Times.Once);
        vacinaAdicionada.Should().NotBeNull();
        vacinaAdicionada!.EventoClinico.IdTipoEvento.Should().Be(IdTipoEventoVacinaSeed);
        // Regressão: id antigo hardcoded (1L) colidia com CONSULTA — não deve mais colidir.
        vacinaAdicionada.EventoClinico.IdTipoEvento.Should().NotBe(1L);
    }

    [Fact]
    public async Task CreateAsync_CdTipoNaoEncontrado_PropagaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("VACINA"))
            .ThrowsAsync(new EntidadeNaoEncontradaException("TipoEvento", "VACINA"));

        var act = async () => await _sut.CreateAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*TipoEvento*VACINA*");

        _vacinaRepoMock.Verify(r => r.AddAsync(It.IsAny<Vacina>()), Times.Never);
    }
}
