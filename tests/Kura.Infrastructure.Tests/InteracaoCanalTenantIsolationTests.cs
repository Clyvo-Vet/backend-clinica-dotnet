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
/// O isolamento real desses endpoints vem do escopo explícito no LINQ do
/// LunaService/TutorService (deriva ID_CLINICA do tutor) — não deste query filter, que
/// é só defesa em profundidade para uma futura leitura autenticada desta tabela.
/// Mesmo padrão de MetricsControllerTenantScopeTests/TenantFilterCoverageTests.
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
    public async Task ComContextoDeClinica_RetornaApenasInteracoesDaquelaClinica()
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
        // nesse cenário — é por isso que LunaService/TutorService escopam
        // explicitamente por IdClinica no LINQ ao ler/derivar (ver LunaService e
        // TutorRepository.GetByTelefoneAsync), em vez de confiar neste filtro.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var interacoes = await ctx.InteracoesCanal.AsNoTracking().ToListAsync();

        interacoes.Should().HaveCount(2,
            "sem contexto de clínica o filtro desliga inteiro (não nega) — comportamento " +
            "documentado, não bug. O isolamento real vem do escopo explícito no service.");
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
