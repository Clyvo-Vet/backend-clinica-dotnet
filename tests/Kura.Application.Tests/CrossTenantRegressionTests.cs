namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// TASK-21 — Testes de regressão para o vazamento cross-tenant em Tutor
/// (GET /api/v1/tutores retornava CPF/e-mail/telefone de tutores de outras clínicas).
///
/// Diferente dos testes de TutorServiceTests.cs (que mockam ITutorRepository), estes testes
/// usam um KuraDbContext real (InMemory) para provar que o isolamento de tenant funciona de
/// ponta a ponta — tanto pelo HasQueryFilter consolidado em KuraDbContext quanto pela defesa
/// em profundidade adicionada em TutorService (idClinica explícito no repositório).
///
/// Inclui também um teste de regressão equivalente para Agendamento, provando que o padrão
/// já usado em AgendaService (passar _clinicaContext.IdClinica manualmente ao repositório,
/// sem depender de HasQueryFilter — Agendamento não tem um) continua funcionando.
/// </summary>
public class CrossTenantRegressionTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    private static KuraDbContext CreateContext(string dbName, long? idClinicaFiltro)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static TutorService BuildTutorService(KuraDbContext ctx, long idClinica)
    {
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(idClinica);

        return new TutorService(
            new TutorRepository(ctx),
            new TutorPetRepository(ctx),
            new Mock<IRepository<Especie>>().Object,
            new Mock<IRepository<Raca>>().Object,
            new Mock<IInviteTutorRepository>().Object,
            new UnitOfWork(ctx),
            clinicaContextMock.Object);
    }

    // ---------- Tutor: isolamento cross-tenant ----------

    [Fact]
    public async Task SearchAsync_ClinicaA_NaoRetornaTutoresDaClinicaB()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed: usa um contexto sem filtro de clínica (simula bypass administrativo de seed).
        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.AddRange(
                new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true },
                new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        var resultado = await sut.SearchAsync(busca: null);

        resultado.Should().ContainSingle();
        resultado.Should().OnlyContain(t => t.NmTutor == "Maria Silva");
        resultado.Should().NotContain(t => t.NmTutor == "João Souza");
    }

    [Fact]
    public async Task SearchAsync_ComBusca_ClinicaA_NaoRetornaTutorDaClinicaBMesmoQuandoTextoBate()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            // Mesmo nome nas duas clínicas — só a diferença de ID_CLINICA deve decidir a visibilidade.
            seedCtx.Tutores.AddRange(
                new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Carlos Pereira", NrCpf = "33333333333", DsEmail = "carlos@a.com", NrTelefone = "11900000003", StAtiva = true },
                new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "Carlos Pereira", NrCpf = "44444444444", DsEmail = "carlos@b.com", NrTelefone = "11900000004", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        var resultado = await sut.SearchAsync(busca: "Carlos");

        resultado.Should().ContainSingle();
        resultado.Single().NrCpf.Should().Be("33333333333");
    }

    [Fact]
    public async Task GetByIdAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        var act = async () => await sut.GetByIdAsync(2L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task GetByIdAsync_TutorDaMesmaClinica_RetornaNormalmente()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        var resultado = await sut.GetByIdAsync(1L);

        resultado.NmTutor.Should().Be("Maria Silva");
    }

    [Fact]
    public async Task GetPetsAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Antes da TASK-21, isto vazava a lista de pets (e, por consequência, o vínculo com o
        // tutor da clínica B) para qualquer clínica autenticada.
        var act = async () => await sut.GetPetsAsync(2L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- Tutor: soft delete continua funcionando após consolidação do HasQueryFilter ----------

    [Fact]
    public async Task SoftDelete_TutorInativado_SomeDoSearchAsyncEDoGetByIdAsync()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        // Confirma que o tutor aparece normalmente antes do soft delete.
        await using (var ctxAntes = CreateContext(dbName, idClinicaFiltro: ClinicaA))
        {
            var sutAntes = BuildTutorService(ctxAntes, ClinicaA);
            (await sutAntes.SearchAsync(null)).Should().ContainSingle();
        }

        // Soft delete via repositório real (mesmo caminho usado por TutorService.SoftDeleteAsync).
        await using (var ctxDelete = CreateContext(dbName, idClinicaFiltro: null))
        {
            var repo = new TutorRepository(ctxDelete);
            var tutor = await repo.GetByIdAsync(1L, ClinicaA);
            tutor.Should().NotBeNull();
            repo.SoftDelete(tutor!);
            await ctxDelete.SaveChangesAsync();
        }

        // Após o soft delete, o tutor não deve mais aparecer em nenhuma leitura da clínica A.
        await using var ctxDepois = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sutDepois = BuildTutorService(ctxDepois, ClinicaA);

        (await sutDepois.SearchAsync(null)).Should().BeEmpty();

        var act = async () => await sutDepois.GetByIdAsync(1L);
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- Agendamento: regressão — AgendaService já protege corretamente (sem HasQueryFilter) ----------

    [Fact]
    public async Task AgendaService_AtualizarStatus_AgendamentoDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 10,
                IdClinica = ClinicaB,
                DtAgendamento = DateTime.UtcNow,
                StStatus = "CONFIRMADO",
                NrVersion = 1,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null); // Agendamento não tem HasQueryFilter — prova que a proteção é 100% manual.
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(ClinicaA);

        var sut = new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctxClinicaA),
            new UnitOfWork(ctxClinicaA));

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };
        var act = async () => await sut.AtualizarStatusAsync(10L, dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AgendaService_AtualizarStatus_AgendamentoDaMesmaClinica_AtualizaNormalmente()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 20,
                IdClinica = ClinicaA,
                DtAgendamento = DateTime.UtcNow,
                StStatus = "CONFIRMADO",
                NrVersion = 1,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(ClinicaA);

        var sut = new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctxClinicaA),
            new UnitOfWork(ctxClinicaA));

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };
        var result = await sut.AtualizarStatusAsync(20L, dto);

        result.DsStatus.Should().Be("REALIZADO");
    }
}
