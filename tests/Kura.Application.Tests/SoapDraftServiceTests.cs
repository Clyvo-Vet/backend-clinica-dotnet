namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Transcricao;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class SoapDraftServiceTests
{
    private readonly Mock<IEventoClinicoRepository> _repositoryMock = new();
    private readonly Mock<ILunaTranscricaoService> _lunaTranscricaoServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly SoapDraftService _sut;

    public SoapDraftServiceTests()
    {
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _sut = new SoapDraftService(
            _repositoryMock.Object,
            _lunaTranscricaoServiceMock.Object,
            _uowMock.Object);
    }

    private static EventoClinico EventoSemDraft() => new()
    {
        Id = 10,
        IdClinica = 1,
        IdPet = 5,
        IdVeterinario = 3,
        IdTipoEvento = 1,
        DsObservacao = "Consulta de rotina",
        StSoapConfirmado = false
    };

    // ---------- EnviarTranscricaoAsync ----------

    [Fact]
    public async Task EnviarTranscricaoAsync_ComSucessoNaLuna_SalvaDraftNaoConfirmado()
    {
        var evento = EventoSemDraft();
        _repositoryMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(evento);
        _lunaTranscricaoServiceMock
            .Setup(l => l.TranscreverAsync(It.IsAny<Stream>(), "audio.mp3", "audio/mpeg"))
            .ReturnsAsync(new TranscricaoResultDto
            {
                Transcricao = "paciente apresenta febre",
                Soap = new SoapDraftDto { S = "s", O = "o", A = "a", P = "p" }
            });

        using var stream = new MemoryStream();
        var result = await _sut.EnviarTranscricaoAsync(10L, stream, "audio.mp3", "audio/mpeg");

        result.DsTranscricao.Should().Be("paciente apresenta febre");
        result.Soap.S.Should().Be("s");
        result.Soap.O.Should().Be("o");
        result.Soap.A.Should().Be("a");
        result.Soap.P.Should().Be("p");
        result.StSoapConfirmado.Should().BeFalse();
        evento.StSoapConfirmado.Should().BeFalse();
        _repositoryMock.Verify(r => r.Update(evento), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task EnviarTranscricaoAsync_LunaFalha_SalvaDraftVazioSemCrashENaoConfirmado()
    {
        var evento = EventoSemDraft();
        _repositoryMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(evento);
        _lunaTranscricaoServiceMock
            .Setup(l => l.TranscreverAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new TranscricaoResultDto());

        using var stream = new MemoryStream();
        var result = await _sut.EnviarTranscricaoAsync(10L, stream, "audio.mp3", "audio/mpeg");

        result.DsTranscricao.Should().BeNull();
        result.Soap.S.Should().BeNull();
        result.Soap.O.Should().BeNull();
        result.Soap.A.Should().BeNull();
        result.Soap.P.Should().BeNull();
        result.StSoapConfirmado.Should().BeFalse();
        _repositoryMock.Verify(r => r.Update(evento), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task EnviarTranscricaoAsync_EventoInexistente_LancaEntidadeNaoEncontradaSemChamarLuna()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((EventoClinico?)null);

        using var stream = new MemoryStream();
        var act = async () => await _sut.EnviarTranscricaoAsync(99L, stream, "audio.mp3", "audio/mpeg");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _lunaTranscricaoServiceMock.Verify(
            l => l.TranscreverAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    // ---------- ConfirmarSoapAsync ----------

    [Fact]
    public async Task ConfirmarSoapAsync_ComDraftSoapPendente_GravaTextoRevisadoEMarcaConfirmado()
    {
        var evento = EventoSemDraft();
        evento.DsSoapS = "draft s";
        evento.DsSoapO = "draft o";
        evento.DsSoapA = "draft a";
        evento.DsSoapP = "draft p";
        _repositoryMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(evento);

        var dto = new SoapConfirmarDto { S = "final s", O = "final o", A = "final a", P = "final p" };
        var result = await _sut.ConfirmarSoapAsync(10L, dto);

        result.Soap.S.Should().Be("final s");
        result.Soap.O.Should().Be("final o");
        result.Soap.A.Should().Be("final a");
        result.Soap.P.Should().Be("final p");
        result.StSoapConfirmado.Should().BeTrue();
        evento.StSoapConfirmado.Should().BeTrue();
        _repositoryMock.Verify(r => r.Update(evento), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfirmarSoapAsync_EventoInexistente_LancaEntidadeNaoEncontrada()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((EventoClinico?)null);

        var act = async () => await _sut.ConfirmarSoapAsync(99L, new SoapConfirmarDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }
}
