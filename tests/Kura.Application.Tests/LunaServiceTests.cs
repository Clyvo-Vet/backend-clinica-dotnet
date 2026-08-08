namespace Kura.Application.Tests;

using System.Text.Json;
using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class LunaServiceTests
{
    private readonly Mock<ITriagemLunaRepository> _triagemRepoMock = new();
    private readonly Mock<IRepository<InteracaoCanal>> _interacaoRepoMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly LunaService _sut;

    public LunaServiceTests()
    {
        _sut = new LunaService(
            _triagemRepoMock.Object,
            _interacaoRepoMock.Object,
            _tutorRepoMock.Object,
            _uowMock.Object);
    }

    private static DateTime Inicio => new(2026, 5, 1);
    private static DateTime Fim => new(2026, 5, 31);

    // ── GerarRelatorioAsync (pré-existente, TASK-05) ────────────────────────

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloValido_RetornaAgregacaoCorreta()
    {
        var triagens = new List<TriagemLuna>
        {
            new() { Id = 1, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(1), StAtiva = true, DsDescricao = "desc" },
            new() { Id = 2, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(2), StAtiva = true, DsDescricao = "desc" },
            new() { Id = 3, IdClinica = 1, DsNivelUrgencia = "LEVE", StEncaminhadoVet = false, DtTriagem = Inicio.AddDays(3), StAtiva = true, DsDescricao = "desc" },
        };

        _triagemRepoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(triagens);

        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        result.Should().NotBeNull();
        result.TotalTriagens.Should().Be(3);
        result.EncaminhadasParaVet.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("URGENTE").WhoseValue.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("LEVE").WhoseValue.Should().Be(1);
        result.DataInicio.Should().Be(Inicio);
        result.DataFim.Should().Be(Fim);
    }

    [Fact]
    public async Task GerarRelatorioAsync_SemTriagensNoPeriodo_RetornaZeros()
    {
        _triagemRepoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(new List<TriagemLuna>());

        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        result.TotalTriagens.Should().Be(0);
        result.EncaminhadasParaVet.Should().Be(0);
        result.PorUrgencia.Should().BeEmpty();
    }

    [Fact]
    public async Task GerarRelatorioAsync_DataFimAnteriorDataInicio_LancaRegraDeNegocio()
    {
        var act = async () => await _sut.GerarRelatorioAsync(Fim, Inicio);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("DataFim não pode ser anterior à DataInicio.");
    }

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloMaiorQue90Dias_LancaRegraDeNegocio()
    {
        var inicio = new DateTime(2026, 1, 1);
        var fimFora = inicio.AddDays(91);

        var act = async () => await _sut.GerarRelatorioAsync(inicio, fimFora);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Intervalo máximo de 90 dias.");
    }

    // ── RegistrarInteracaoAsync (TASK-67) ───────────────────────────────────

    private static Tutor TutorClinica42(long id = 7) => new()
    {
        Id = id,
        IdClinica = 42,
        NmTutor = "Fulano",
        NrCpf = "11122233344",
        DsEmail = "fulano@teste.com",
        NrTelefone = "5511999990000",
        StAtiva = true
    };

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorNull_Lanca422SemMencionarPayload()
    {
        // Teste que morde: um mapeamento ingênuo (gravar ID_CLINICA = 0/null direto)
        // estouraria ORA-01400 (NOT NULL) no Oracle real — 500. Este teste prova que o
        // service intercepta ANTES de chegar no banco.
        var dto = new InteractionRequestDto
        {
            IdTutor = null,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "MARCADOR_LGPD_conteudo_sensivel_x7f2",
            DtRecebimento = DateTime.UtcNow
        };

        var act = async () => await _sut.RegistrarInteracaoAsync(dto);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().NotContain("MARCADOR_LGPD_conteudo_sensivel_x7f2",
            "a mensagem de exceção não pode vazar ds_conteudo — ela sobe até o " +
            "ExceptionHandlerMiddleware, que loga ex.Message e o devolve no corpo HTTP");

        _interacaoRepoMock.Verify(r => r.AddAsync(It.IsAny<InteracaoCanal>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorInexistente_Lanca404()
    {
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Tutor?)null);

        var dto = new InteractionRequestDto
        {
            IdTutor = 99,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "oi",
            DtRecebimento = DateTime.UtcNow
        };

        var act = async () => await _sut.RegistrarInteracaoAsync(dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_TutorValido_DerivaIdClinicaDoTutor()
    {
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "Meu pet está com febre",
            DtRecebimento = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc)
        };

        await _sut.RegistrarInteracaoAsync(dto);

        capturada.Should().NotBeNull();
        capturada!.IdClinica.Should().Be(42, "ID_CLINICA é NOT NULL e a Luna nunca envia — só dá pra derivar do tutor");
        capturada.IdTutor.Should().Be(tutor.Id);
        capturada.DsConteudo.Should().Be("Meu pet está com febre");
        capturada.DsMetadados.Should().BeNull();
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ConteudoMaiorQue4000_Trunca()
    {
        // DS_CONTEUDO é VARCHAR2(4000) NOT NULL — sem truncar, o Oracle real
        // estouraria (na verdade Oracle trunca com erro ORA-12899, "value too large
        // for column"), não silenciosamente. Truncar aqui evita o 500 completamente.
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        var conteudoGigante = new string('a', 5000);
        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = conteudoGigante,
            DtRecebimento = DateTime.UtcNow
        };

        await _sut.RegistrarInteracaoAsync(dto);

        capturada!.DsConteudo.Length.Should().Be(4000);
        capturada.DsConteudo.Should().Be(conteudoGigante[..4000]);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ComMetadados_SerializaJsonBrutoNoClob()
    {
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        using var doc = JsonDocument.Parse("""{"media_id":"abc123"}""");
        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "oi",
            DtRecebimento = DateTime.UtcNow,
            DsMetadados = doc.RootElement.Clone()
        };

        await _sut.RegistrarInteracaoAsync(dto);

        capturada!.DsMetadados.Should().Be("""{"media_id":"abc123"}""");
    }

    // ── RegistrarTriagemAsync (TASK-67) ─────────────────────────────────────

    private static InteracaoCanal InteracaoExistente(long id = 100, long idClinica = 42) => new()
    {
        Id = id,
        IdClinica = idClinica,
        IdTutor = 7,
        DsCanal = "WHATSAPP",
        DsDirecao = "INBOUND",
        DsConteudo = "oi",
        DtRecebimento = DateTime.UtcNow,
        StAtiva = true
    };

    [Fact]
    public async Task RegistrarTriagemAsync_PayloadRealDaLuna_NaoLancaEComponeDescricaoSemPerderDados()
    {
        // Teste que morde: TriageRequestDTO real da Luna manda sintomas[]/nr_score/
        // ds_recomendacao (sem coluna própria) e NÃO manda DS_DESCRICAO nem DT_TRIAGEM
        // (NOT NULL em TRIAGEM_LUNA, V9). Um mapeamento ingênuo (TriagemLuna { DsDescricao
        // = dto.DsDescricao }) nem compila — a versão que só ignora sintomas/score/
        // recomendacao perderia dado sem gravar em lugar nenhum. Este teste prova as
        // duas coisas: não lança, E os 3 campos aparecem em algum lugar da linha gravada.
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);

        TriagemLuna? capturada = null;
        _triagemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<TriagemLuna>()))
            .Callback<TriagemLuna>(t => capturada = t)
            .Returns(Task.CompletedTask);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito", "letargia"],
            DsUrgencia = "ALTA",
            NrScore = 87,
            DsRecomendacao = "Levar ao veterinário em até 2 horas"
        };

        var act = async () => await _sut.RegistrarTriagemAsync(dto);
        await act.Should().NotThrowAsync();

        capturada.Should().NotBeNull();
        capturada!.DsDescricao.Should().NotBeNullOrEmpty("DS_DESCRICAO é NOT NULL em TRIAGEM_LUNA");
        capturada.DsDescricao.Should().Contain("vomito").And.Contain("letargia").And.Contain("87").And.Contain("Levar ao veterinário");
        capturada.DsDescricao.Length.Should().BeLessThanOrEqualTo(2000, "DS_DESCRICAO é VARCHAR2(2000)");
        capturada.DtTriagem.Should().NotBe(default(DateTime), "DT_TRIAGEM é NOT NULL e não vem do payload — precisa de coalesce no service");
        capturada.IdClinica.Should().Be(42, "derivado do tutor, mesmo padrão de RegistrarInteracaoAsync");
        capturada.IdInteracao.Should().Be(interacao.Id);
        capturada.DsNivelUrgencia.Should().Be("ALTA");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_DescricaoMaiorQue2000_Trunca()
    {
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);

        TriagemLuna? capturada = null;
        _triagemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<TriagemLuna>()))
            .Callback<TriagemLuna>(t => capturada = t)
            .Returns(Task.CompletedTask);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = Enumerable.Range(0, 400).Select(i => $"sintoma{i}").ToList(),
            DsUrgencia = "BAIXA",
            NrScore = 10,
            DsRecomendacao = new string('x', 3000)
        };

        await _sut.RegistrarTriagemAsync(dto);

        capturada!.DsDescricao.Length.Should().Be(2000);
    }

    [Fact]
    public async Task RegistrarTriagemAsync_InteracaoInexistente_Lanca404()
    {
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((InteracaoCanal?)null);

        var dto = new TriageRequestDto
        {
            IdInteracao = 999,
            IdTutor = 7,
            Sintomas = ["tosse"],
            DsUrgencia = "BAIXA",
            NrScore = 5,
            DsRecomendacao = "observar"
        };

        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>(
            "gravar TriagemLuna.IdInteracao apontando pra uma interação inexistente " +
            "estouraria a FK do Oracle (ORA-02291) se não fosse checado antes — 500");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_TutorInexistente_Lanca404()
    {
        var interacao = InteracaoExistente();
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(555)).ReturnsAsync((Tutor?)null);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = 555,
            Sintomas = ["tosse"],
            DsUrgencia = "BAIXA",
            NrScore = 5,
            DsRecomendacao = "observar"
        };

        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
