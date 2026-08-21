namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-67: prova o isolamento de tenant de INTERACAO_CANAL (entidade nova) e a
/// armadilha documentada em CLAUDE.md/TenantFilterCoverageTests — o filtro DESLIGA
/// inteiro (não nega) quando IdClinicaFiltro é null, que é sempre o caso real para os
/// 3 endpoints consumidos pela Luna (autenticados por API Key, sem JWT de clínica).
///
/// CORREÇÃO (fix round 1, Important-3 da revisão): esta classe dizia que "o isolamento
/// real desses endpoints vem do escopo explícito no LINQ do LunaService/TutorService" —
/// impreciso. As leituras desses services são <c>GetByIdAsync</c> por PK
/// (<c>FindAsync</c>), não uma query LINQ escopada por IdClinica — e o revisor
/// confirmou por teste isolado que <c>FindAsync</c> aplica os query filters
/// normalmente (não é o problema). O que de fato garante isolamento aqui: o
/// <c>ID_CLINICA</c> da linha ESCRITA é sempre derivado do tutor (correto e suficiente
/// para essa coluna), e — desde este fix — <c>LunaService.RegistrarTriagemAsync</c>
/// valida explicitamente que <c>interacao.IdClinica == tutor.IdClinica</c> antes de
/// gravar a FK, porque este query filter sozinho (inerte sem JWT) não pega esse tipo
/// de inconsistência cross-tenant. Mesmo padrão de
/// MetricsControllerTenantScopeTests/TenantFilterCoverageTests.
/// </summary>
public class InteracaoCanalTenantIsolationTests
{
    private static KuraDbContext CreateContext(long? idClinicaFiltro, string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static async Task SeedDuasClinicasAsync(string dbName)
    {
        using var seedCtx = CreateContext(idClinicaFiltro: null, dbName);

        seedCtx.Clinicas.AddRange(
            new Clinica
            {
                Id = 1, NmClinica = "Clinica A", NrCnpj = "00000000000101", DsEndereco = "Rua A, 1",
                NmCidade = "Sao Paulo", SgUf = "SP", NrCep = "00000001", DsEmail = "a@teste.com",
                DsEmailAcesso = "a@teste.com", DsSenhaHash = "hash", StAtiva = true
            },
            new Clinica
            {
                Id = 2, NmClinica = "Clinica B", NrCnpj = "00000000000102", DsEndereco = "Rua B, 2",
                NmCidade = "Rio de Janeiro", SgUf = "RJ", NrCep = "00000002", DsEmail = "b@teste.com",
                DsEmailAcesso = "b@teste.com", DsSenhaHash = "hash", StAtiva = true
            });

        seedCtx.InteracoesCanal.AddRange(
            new InteracaoCanal
            {
                Id = 1, IdClinica = 1, DsCanal = "WHATSAPP", DsDirecao = "INBOUND",
                DsConteudo = "clinica A", DtRecebimento = DateTime.UtcNow, StAtiva = true
            },
            new InteracaoCanal
            {
                Id = 2, IdClinica = 2, DsCanal = "WHATSAPP", DsDirecao = "INBOUND",
                DsConteudo = "clinica B", DtRecebimento = DateTime.UtcNow, StAtiva = true
            });

        await seedCtx.SaveChangesAsync();
    }

    [Fact]
    public async Task ComContextoDeClinica_FiltroIsolaPorClinica_RetornaApenasInteracoesDaquelaClinica()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().ContainSingle()
            .Which.IdClinica.Should().Be(1);
    }

    [Fact]
    public async Task SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas()
    {
        // Este é o caso REAL dos 3 endpoints da Luna: chamada por API Key, sem JWT ⇒
        // IdClinicaFiltro é sempre null aqui. O query filter, sozinho, NÃO isola nada
        // nesse cenário. O que compensa: ID_CLINICA da linha escrita vem sempre do
        // tutor (LunaService/TutorRepository.GetByTelefoneAsync), e a consistência da
        // FK ID_INTERACAO é checada explicitamente em
        // LunaService.RegistrarTriagemAsync (interacao.IdClinica == tutor.IdClinica) —
        // nenhuma das duas coisas é "escopo no LINQ": são checagens depois de ler por
        // PK, não uma query filtrada.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().HaveCount(2,
            "sem contexto de clínica o filtro desliga inteiro (não nega) — comportamento " +
            "documentado, não bug. A compensação real vem da derivação de ID_CLINICA a " +
            "partir do tutor mais a checagem cross-FK em RegistrarTriagemAsync, não deste filtro.");
    }

    [Fact]
    public async Task SemJwt_InteracaoSemClinica_SempreAparece()
    {
        // TASK-77 (FIX_7): interação de tutor não identificado grava com IdClinica
        // null. Sem JWT de clínica (IdClinicaFiltro null — o caso real dos 3 endpoints
        // da Luna, autenticados por API Key), o filtro desliga inteiro, exatamente como
        // já provado para linhas COM clínica em SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas
        // acima. Esta linha deve aparecer junto com as demais.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.InteracoesCanal.Add(new InteracaoCanal
            {
                Id = 3, IdClinica = null, DsCanal = "WHATSAPP", DsDirecao = "INBOUND",
                DsConteudo = "tutor desconhecido", DtRecebimento = DateTime.UtcNow, StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().HaveCount(3,
            "sem JWT o filtro desliga inteiro — a interação sem clínica atribuída deve " +
            "aparecer junto com as duas de clínica conhecida");
        interacoes.Should().ContainSingle(i => i.Id == 3)
            .Which.IdClinica.Should().BeNull();
    }

    [Fact]
    public async Task ComJwt_InteracaoSemClinica_NuncaAparece()
    {
        // TASK-77 (FIX_7) — este é o comportamento que precisava ser PROVADO, não
        // assumido: com IdClinicaFiltro preenchido (JWT de clínica real, hoje só usado
        // por leitura futura desta tabela — os 3 endpoints da Luna não passam JWT), a
        // comparação `e.IdClinica == filtro` passa a ser nullable == não-nullable. O
        // predicado em ApplyTenantFilters escreve `e.IdClinica != null && ...` de
        // propósito, sem depender da tradução de null do EF Core — este teste prova o
        // resultado real (contra InMemory; Oracle real é responsabilidade do G4 do
        // ciclo, não desta suíte).
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.InteracoesCanal.Add(new InteracaoCanal
            {
                Id = 3, IdClinica = null, DsCanal = "WHATSAPP", DsDirecao = "INBOUND",
                DsConteudo = "tutor desconhecido", DtRecebimento = DateTime.UtcNow, StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().ContainSingle()
            .Which.IdClinica.Should().Be(1,
                "com JWT de clínica 1, a interação sem clínica atribuída (Id=3) NÃO pode " +
                "aparecer — é a consequência aceita e documentada da decisão de produto " +
                "(ganho é auditoria, não visibilidade no app)");
    }

    [Fact]
    public async Task StAtivaFalse_NuncaAparece_MesmoSemContextoDeClinica()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.Clinicas.Add(new Clinica
            {
                Id = 1, NmClinica = "Clinica A", NrCnpj = "00000000000101", DsEndereco = "Rua A, 1",
                NmCidade = "Sao Paulo", SgUf = "SP", NrCep = "00000001", DsEmail = "a@teste.com",
                DsEmailAcesso = "a@teste.com", DsSenhaHash = "hash", StAtiva = true
            });
            seedCtx.InteracoesCanal.Add(new InteracaoCanal
            {
                Id = 1, IdClinica = 1, DsCanal = "WHATSAPP", DsDirecao = "INBOUND",
                DsConteudo = "inativa", DtRecebimento = DateTime.UtcNow, StAtiva = false
            });
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().BeEmpty("StAtiva=false é soft delete — a parte StAtiva do filtro continua valendo mesmo sem contexto de clínica");
    }
}
