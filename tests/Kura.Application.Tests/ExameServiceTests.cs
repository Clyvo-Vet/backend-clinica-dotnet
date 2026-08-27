namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Exame;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class ExameServiceTests
{
    // Ids da seed real (afterMigrate__seeds_dev.sql): CONSULTA=1, TELEORIENTACAO=2, VACINA=3,
    // PRESCRICAO=4, EXAME=5. O antigo const hardcoded usava IdTipoEventoExame=3, que colidia com
    // VACINA — este teste garante a resolução correta e distinta por CD_TIPO.
    private const long IdTipoEventoExameSeed = 5L;

    private readonly Mock<IEventoClinicoRepository> _eventoRepoMock = new();
    private readonly Mock<IRepository<Exame>> _exameRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<ITipoEventoService> _tipoEventoServiceMock = new();
    private readonly ExameService _sut;

    public ExameServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("EXAME"))
            .ReturnsAsync(IdTipoEventoExameSeed);
        _sut = new ExameService(
            _eventoRepoMock.Object,
            _exameRepoMock.Object,
            _uowMock.Object,
            _clinicaMock.Object,
            _tipoEventoServiceMock.Object);
    }

    private static ExameCreateDto ValidDto(string dsObservacao = "") => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtEvento = new DateTime(2026, 5, 6, 9, 0, 0),
        NmExame = "Hemograma completo",
        DsResultado = "Dentro dos padrões",
        DtRealizacao = new DateTime(2026, 5, 6),
        DsObservacao = dsObservacao
    };

    [Fact]
    public async Task CreateAsync_ResolveIdTipoEventoPorCdTipo_PersisteFkDistintaDeOutrosTipos()
    {
        // Arrange
        Exame? exameAdicionado = null;
        _exameRepoMock.Setup(r => r.AddAsync(It.IsAny<Exame>()))
            .Callback<Exame>(e => exameAdicionado = e)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidDto());

        // Assert
        _tipoEventoServiceMock.Verify(t => t.GetIdByCdTipoAsync("EXAME"), Times.Once);
        exameAdicionado.Should().NotBeNull();
        exameAdicionado!.EventoClinico.IdTipoEvento.Should().Be(IdTipoEventoExameSeed);
        // Regressão: id antigo hardcoded (3L) colidia com VACINA — não deve mais colidir.
        exameAdicionado.EventoClinico.IdTipoEvento.Should().NotBe(3L);
    }

    [Fact]
    public async Task CreateAsync_CdTipoNaoEncontrado_PropagaEntidadeNaoEncontrada()
    {
        // Arrange
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("EXAME"))
            .ThrowsAsync(new EntidadeNaoEncontradaException("TipoEvento", "EXAME"));

        // Act
        var act = async () => await _sut.CreateAsync(ValidDto());

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*TipoEvento*EXAME*");

        _exameRepoMock.Verify(r => r.AddAsync(It.IsAny<Exame>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_DsObservacaoVaziaOuWhitespace_ColescaParaSentinela(string dsObservacaoBruta)
    {
        // Arrange
        // TASK-56: EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL (V9:58, migration imutável) e o Oracle
        // trata VARCHAR2 vazio como NULL — sem o coalesce no service, o payload real do app
        // (mobile-clinica-rn/src/app/(app)/receituario/[idPet].tsx) não manda dsObservacao e o
        // INSERT estoura ORA-01400 (500).
        Exame? exameAdicionado = null;
        _exameRepoMock.Setup(r => r.AddAsync(It.IsAny<Exame>()))
            .Callback<Exame>(e => exameAdicionado = e)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(dsObservacao: dsObservacaoBruta);

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        exameAdicionado.Should().NotBeNull();
        exameAdicionado!.EventoClinico.DsObservacao.Should().Be("Sem observações");
    }

    [Fact]
    public async Task CreateAsync_DsObservacaoPreenchida_NaoSobrescreveComSentinela()
    {
        // Arrange
        Exame? exameAdicionado = null;
        _exameRepoMock.Setup(r => r.AddAsync(It.IsAny<Exame>()))
            .Callback<Exame>(e => exameAdicionado = e)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(dsObservacao: "Coleta realizada em jejum");

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        exameAdicionado.Should().NotBeNull();
        exameAdicionado!.EventoClinico.DsObservacao.Should().Be("Coleta realizada em jejum");
    }
}
