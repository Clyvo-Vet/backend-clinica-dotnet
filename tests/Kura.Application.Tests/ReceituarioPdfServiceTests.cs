namespace Kura.Application.Tests;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class ReceituarioPdfServiceTests : IDisposable
{
    // QuestPDF exige a declaração da licença uma vez por processo (normalmente feita em
    // Program.cs, que os testes não executam) — Community é gratuita para receita < US$1M.
    static ReceituarioPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private readonly Mock<IEventoClinicoRepository> _eventoRepoMock = new();
    private readonly Mock<IRepository<Prescricao>> _prescricaoRepoMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _veterinarioRepoMock = new();
    private readonly Mock<IRepository<Medicamento>> _medicamentoRepoMock = new();
    private readonly Mock<IRepository<Documento>> _documentoRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly string _storageDir;
    private readonly ReceituarioPdfService _sut;

    public ReceituarioPdfServiceTests()
    {
        _storageDir = Path.Combine(Path.GetTempPath(), "kura-tests-receituario-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BasePath"] = _storageDir })
            .Build();

        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Documento>()))
            .Callback<Documento>(d => d.Id = 55)
            .Returns(Task.CompletedTask);

        _sut = new ReceituarioPdfService(
            _eventoRepoMock.Object,
            _prescricaoRepoMock.Object,
            _petRepoMock.Object,
            _veterinarioRepoMock.Object,
            _medicamentoRepoMock.Object,
            _documentoRepoMock.Object,
            _uowMock.Object,
            configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

    private static EventoClinico Evento() => new()
    {
        Id = 10,
        IdClinica = 1,
        IdPet = 5,
        IdVeterinario = 3,
        IdTipoEvento = 2,
        DtEvento = new DateTime(2026, 7, 30),
        DsObservacao = "Prescrição de antibiótico",
    };

    private static Prescricao Prescricao() => new()
    {
        Id = 20,
        IdEventoClinico = 10,
        IdMedicamento = 7,
        DsPosologia = "1 comprimido a cada 12h",
        NrDuracaoDias = 7,
    };

    private static Pet Pet() => new()
    {
        Id = 5,
        IdClinica = 1,
        IdEspecie = 1,
        IdRaca = 1,
        NmPet = "Thor",
        DtNascimento = new DateTime(2020, 1, 1),
        SgSexo = 'M',
        SgPorte = 'G',
    };

    private static Veterinario Veterinario() => new()
    {
        Id = 3,
        IdClinica = 1,
        NmVeterinario = "Dr. Felipe Ferrete",
        NrCrmv = "SP-12345",
        DsEmail = "felipe@kura.com",
        NrTelefone = "11999990000",
    };

    private static Medicamento Medicamento() => new()
    {
        Id = 7,
        NmMedicamento = "Amoxicilina",
        DsPrincipioAtivo = "Amoxicilina triidratada",
        DsApresentacao = "Comprimido 500mg",
    };

    private void SetupCaminhoFeliz()
    {
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());
        _prescricaoRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Prescricao, bool>>>()))
            .ReturnsAsync(new[] { Prescricao() });
        _petRepoMock.Setup(r => r.GetByIdAsync(5L)).ReturnsAsync(Pet());
        _veterinarioRepoMock.Setup(r => r.GetByIdAsync(3L)).ReturnsAsync(Veterinario());
        _medicamentoRepoMock.Setup(r => r.GetByIdAsync(7L)).ReturnsAsync(Medicamento());
    }

    [Fact]
    public async Task GerarReceituarioAsync_ComDadosValidos_GeraPdfNaoVazioECriaDocumento()
    {
        SetupCaminhoFeliz();

        var result = await _sut.GerarReceituarioAsync(10L);

        result.Id.Should().Be(55);
        result.IdEventoClinico.Should().Be(10);
        result.DsTipoMime.Should().Be("application/pdf");
        result.NrTamanhoBytes.Should().BeGreaterThan(0);
        result.DsCaminho.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DsCaminho).Should().BeTrue();
        new FileInfo(result.DsCaminho).Length.Should().Be(result.NrTamanhoBytes);

        _documentoRepoMock.Verify(r => r.AddAsync(It.Is<Documento>(d =>
            d.IdEventoClinico == 10 && d.DsTipoMime == "application/pdf" && d.NrTamanhoBytes > 0)), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task GerarReceituarioAsync_EventoInexistente_LancaEntidadeNaoEncontradaSemPersistir()
    {
        _eventoRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((EventoClinico?)null);

        var act = async () => await _sut.GerarReceituarioAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Documento>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task GerarReceituarioAsync_PrescricaoInexistente_LancaEntidadeNaoEncontrada()
    {
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());
        _prescricaoRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Prescricao, bool>>>()))
            .ReturnsAsync(Array.Empty<Prescricao>());

        var act = async () => await _sut.GerarReceituarioAsync(10L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Documento>()), Times.Never);
    }
}
