namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Teleconsulta;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class TeleconsultaServiceTests
{
    private readonly Mock<IAgendamentoRepository> _agendamentoRepoMock = new();
    private readonly Mock<IConsentimentoRepository> _consentimentoRepoMock = new();
    private readonly Mock<IDailyService> _dailyServiceMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly TeleconsultaService _sut;

    public TeleconsultaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _sut = new TeleconsultaService(
            _agendamentoRepoMock.Object,
            _consentimentoRepoMock.Object,
            _dailyServiceMock.Object,
            _clinicaMock.Object,
            _uowMock.Object);
    }

    private static Agendamento AgendamentoSemSala(long idTutor = 5) => new()
    {
        Id = 10,
        IdClinica = 1,
        IdTutor = idTutor,
        StTeleconsulta = false,
        DsSalaUrl = null
    };

    private static Consentimento ConsentimentoAceito() => new()
    {
        Id = 1,
        IdTutor = 5,
        DsTipo = "TELEORIENTACAO",
        StAceito = 'S',
        NrVersaoTermo = "1.0",
        DtConsentimento = DateTime.UtcNow.AddDays(-1)
    };

    private static Consentimento ConsentimentoRecusado() => new()
    {
        Id = 1,
        IdTutor = 5,
        DsTipo = "TELEORIENTACAO",
        StAceito = 'N',
        NrVersaoTermo = "1.0",
        DtConsentimento = DateTime.UtcNow.AddDays(-1)
    };

    // ---------- CriarOuObterSalaAsync ----------

    [Fact]
    public async Task CriarOuObterSalaAsync_ComConsentimentoAceito_CriaSalaEPersiste()
    {
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());
        _dailyServiceMock.Setup(d => d.CriarSalaAsync("kura-agendamento-10"))
            .ReturnsAsync(DailyRoomResult.ComSucesso("https://kura.daily.co/room-10"));

        var result = await _sut.CriarOuObterSalaAsync(10L);

        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        result.DsProvedorVideo.Should().Be("DAILY");
        result.StFallbackManual.Should().BeFalse();
        agendamento.StTeleconsulta.Should().BeTrue();
        _agendamentoRepoMock.Verify(r => r.Update(agendamento), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_SemConsentimentoRegistrado_LancaRegraDeNegocioENaoChamaDaily()
    {
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync((Consentimento?)null);

        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_ConsentimentoRecusado_LancaRegraDeNegocio()
    {
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoRecusado());

        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_AgendamentoSemTutor_TrataComoSemConsentimento()
    {
        var agendamento = AgendamentoSemSala(idTutor: 0);
        agendamento.IdTutor = null;
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _consentimentoRepoMock.Verify(
            r => r.GetMaisRecenteAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_DailyFalha_RetornaFallbackManualSemPersistir()
    {
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());
        _dailyServiceMock.Setup(d => d.CriarSalaAsync(It.IsAny<string>()))
            .ReturnsAsync(DailyRoomResult.Falha());

        var result = await _sut.CriarOuObterSalaAsync(10L);

        result.StFallbackManual.Should().BeTrue();
        result.DsSalaUrl.Should().BeNull();
        agendamento.StTeleconsulta.Should().BeFalse();
        _agendamentoRepoMock.Verify(r => r.Update(It.IsAny<Agendamento>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_SalaJaCriada_RetornaExistenteSemChamarDaily()
    {
        var agendamento = AgendamentoSemSala();
        agendamento.StTeleconsulta = true;
        agendamento.DsSalaUrl = "https://kura.daily.co/room-10";
        agendamento.DsProvedorVideo = "DAILY";
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());

        var result = await _sut.CriarOuObterSalaAsync(10L);

        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_AgendamentoInexistente_LancaEntidadeNaoEncontrada()
    {
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Agendamento?)null);

        var act = async () => await _sut.CriarOuObterSalaAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- ObterSalaAsync ----------

    [Fact]
    public async Task ObterSalaAsync_RetornaEstadoAtualSemChamarDailyOuConsentimento()
    {
        var agendamento = AgendamentoSemSala();
        agendamento.StTeleconsulta = true;
        agendamento.DsSalaUrl = "https://kura.daily.co/room-10";
        agendamento.DsProvedorVideo = "DAILY";
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var result = await _sut.ObterSalaAsync(10L);

        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _consentimentoRepoMock.Verify(
            r => r.GetMaisRecenteAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ObterSalaAsync_AgendamentoInexistente_LancaEntidadeNaoEncontrada()
    {
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Agendamento?)null);

        var act = async () => await _sut.ObterSalaAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
