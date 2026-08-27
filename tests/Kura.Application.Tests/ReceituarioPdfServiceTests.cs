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
        // Arrange
        SetupCaminhoFeliz();

        // Act
        var result = await _sut.GerarReceituarioAsync(10L);

        // Assert
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
        // Arrange
        _eventoRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((EventoClinico?)null);

        // Act
        var act = async () => await _sut.GerarReceituarioAsync(99L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Documento>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task GerarReceituarioAsync_PrescricaoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());
        _prescricaoRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Prescricao, bool>>>()))
            .ReturnsAsync(Array.Empty<Prescricao>());

        // Act
        var act = async () => await _sut.GerarReceituarioAsync(10L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Documento>()), Times.Never);
    }

    // ---------- TASK-51: download dos bytes do PDF já gerado ----------

    [Fact]
    public async Task ObterArquivoReceituarioAsync_ComDocumentoValido_RetornaBytesDoArquivo()
    {
        // Arrange
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());

        Directory.CreateDirectory(_storageDir);
        var caminho = Path.Combine(_storageDir, "receituario-teste.pdf");
        var bytesEsperados = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(caminho, bytesEsperados);

        var documento = new Documento
        {
            Id = 55,
            IdEventoClinico = 10,
            NmArquivo = "receituario-teste.pdf",
            DsTipoMime = "application/pdf",
            DsCaminho = caminho,
            NrTamanhoBytes = bytesEsperados.Length,
        };
        _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync(documento);

        // Act
        var resultado = await _sut.ObterArquivoReceituarioAsync(10L, 55L);

        // Assert
        resultado.Conteudo.Should().Equal(bytesEsperados);
        resultado.NomeArquivo.Should().Be("receituario-teste.pdf");
        resultado.DsTipoMime.Should().Be("application/pdf");
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_EventoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _eventoRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((EventoClinico?)null);

        // Act
        var act = async () => await _sut.ObterArquivoReceituarioAsync(99L, 1L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_DocumentoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());
        _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync((Documento?)null);

        // Act
        var act = async () => await _sut.ObterArquivoReceituarioAsync(10L, 55L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_DocumentoDeOutroEvento_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        // Documento existe no banco, mas pertence a um EventoClinico diferente do
        // informado na rota — não pode ser servido por esta chamada mesmo que o
        // registro exista fisicamente (o id do evento na URL precisa bater com o dono
        // real do documento).
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());

        Directory.CreateDirectory(_storageDir);
        var caminho = Path.Combine(_storageDir, "de-outro-evento.pdf");
        await File.WriteAllBytesAsync(caminho, new byte[] { 1 });

        var documentoDeOutroEvento = new Documento
        {
            Id = 55,
            IdEventoClinico = 999,
            NmArquivo = "de-outro-evento.pdf",
            DsTipoMime = "application/pdf",
            DsCaminho = caminho,
        };
        _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync(documentoDeOutroEvento);

        // Act
        var act = async () => await _sut.ObterArquivoReceituarioAsync(10L, 55L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_ArquivoAusenteNoDisco_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        // Documento existe no banco, mas o arquivo em disco sumiu (ex.: volume
        // resetado) — precisa de um erro claro, não uma FileNotFoundException crua.
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());

        var documento = new Documento
        {
            Id = 55,
            IdEventoClinico = 10,
            NmArquivo = "sumiu.pdf",
            DsTipoMime = "application/pdf",
            DsCaminho = Path.Combine(_storageDir, "sumiu.pdf"),
        };
        _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync(documento);

        // Act
        var act = async () => await _sut.ObterArquivoReceituarioAsync(10L, 55L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_CaminhoComTraversalRelativo_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        // Simula um DsCaminho corrompido/adulterado com segmentos ".." tentando escapar
        // de Storage:BasePath — defesa em profundidade: nunca confiar cegamente no
        // caminho persistido, mesmo vindo do próprio banco.
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());

        var caminhoComTraversal = Path.Combine(_storageDir, "..", "..", "secreto.pdf");
        var documento = new Documento
        {
            Id = 55,
            IdEventoClinico = 10,
            NmArquivo = "secreto.pdf",
            DsTipoMime = "application/pdf",
            DsCaminho = caminhoComTraversal,
        };
        _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync(documento);

        // Act
        var act = async () => await _sut.ObterArquivoReceituarioAsync(10L, 55L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_CaminhoAbsolutoForaDoBasePath_LancaEntidadeNaoEncontrada()
    {
        // Mesmo cenário do teste anterior, mas com um caminho absoluto totalmente fora
        // de Storage:BasePath (ex.: registro corrompido apontando para outro diretório
        // do sistema de arquivos).
        _eventoRepoMock.Setup(r => r.GetByIdAsync(10L)).ReturnsAsync(Evento());

        var caminhoForaDoBasePath = Path.Combine(
            Path.GetTempPath(), "kura-fora-do-storage-" + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(caminhoForaDoBasePath, new byte[] { 9, 9 });

        try
        {
            var documento = new Documento
            {
                Id = 55,
                IdEventoClinico = 10,
                NmArquivo = "malicioso.pdf",
                DsTipoMime = "application/pdf",
                DsCaminho = caminhoForaDoBasePath,
            };
            _documentoRepoMock.Setup(r => r.GetByIdAsync(55L)).ReturnsAsync(documento);

            var act = async () => await _sut.ObterArquivoReceituarioAsync(10L, 55L);

            await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        }
        finally
        {
            File.Delete(caminhoForaDoBasePath);
        }
    }
}
