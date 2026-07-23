namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Prescricao;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class PrescricaoServiceTests
{
    // Ids da seed real (afterMigrate__seeds_dev.sql): CONSULTA=1, TELEORIENTACAO=2, VACINA=3,
    // PRESCRICAO=4, EXAME=5. O antigo const hardcoded usava IdTipoEventoPrescricao=2, que colidia
    // com TELEORIENTACAO — este teste garante a resolução correta e distinta por CD_TIPO.
    private const long IdTipoEventoPrescricaoSeed = 4L;

    private readonly Mock<IEventoClinicoRepository> _eventoRepoMock = new();
    private readonly Mock<IRepository<Prescricao>> _prescricaoRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<ITipoEventoService> _tipoEventoServiceMock = new();
    private readonly PrescricaoService _sut;

    public PrescricaoServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("PRESCRICAO"))
            .ReturnsAsync(IdTipoEventoPrescricaoSeed);
        _sut = new PrescricaoService(
            _eventoRepoMock.Object,
            _prescricaoRepoMock.Object,
            _uowMock.Object,
            _clinicaMock.Object,
            _tipoEventoServiceMock.Object);
    }

    private static PrescricaoCreateDto ValidDto() => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtEvento = new DateTime(2026, 5, 6, 9, 0, 0),
        IdMedicamento = 7,
        DsPosologia = "1 comprimido a cada 12h",
        NrDuracaoDias = 10
    };

    [Fact]
    public async Task CreateAsync_ResolveIdTipoEventoPorCdTipo_PersisteFkDistintaDeOutrosTipos()
    {
        Prescricao? prescricaoAdicionada = null;
        _prescricaoRepoMock.Setup(r => r.AddAsync(It.IsAny<Prescricao>()))
            .Callback<Prescricao>(p => prescricaoAdicionada = p)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto());

        _tipoEventoServiceMock.Verify(t => t.GetIdByCdTipoAsync("PRESCRICAO"), Times.Once);
        prescricaoAdicionada.Should().NotBeNull();
        prescricaoAdicionada!.EventoClinico.IdTipoEvento.Should().Be(IdTipoEventoPrescricaoSeed);
        // Regressão: id antigo hardcoded (2L) colidia com TELEORIENTACAO — não deve mais colidir.
        prescricaoAdicionada.EventoClinico.IdTipoEvento.Should().NotBe(2L);
    }

    [Fact]
    public async Task CreateAsync_CdTipoNaoEncontrado_PropagaEntidadeNaoEncontrada()
    {
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("PRESCRICAO"))
            .ThrowsAsync(new EntidadeNaoEncontradaException("TipoEvento", "PRESCRICAO"));

        var act = async () => await _sut.CreateAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*TipoEvento*PRESCRICAO*");

        _prescricaoRepoMock.Verify(r => r.AddAsync(It.IsAny<Prescricao>()), Times.Never);
    }
}
