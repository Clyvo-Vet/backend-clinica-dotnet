namespace Kura.Infrastructure.Tests;

using System.Linq;
using System.Reflection;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-38: a regra de multi-tenancy já foi documentada errada três vezes neste
/// projeto (histórico em CLAUDE.md — v2.0 dizia filtro global, v3.0 listava
/// "descobertas" que nem tinham IdClinica). O risco real não é a doc estar errada
/// hoje, é a PRÓXIMA entidade com IdClinica esquecer de entrar em
/// KuraDbContext.ApplyTenantFilters e ninguém perceber até uma revisão manual
/// meses depois (foi assim que Tutor vazou PII até a TASK-21).
///
/// Este teste troca "lembrar de conferir" por "o build quebra": por reflection
/// sobre Kura.Domain.Entities, descobre toda entidade que declara IdClinica e
/// garante que ela está em ApplyTenantFilters OU na allowlist explícita abaixo
/// (compensação manual, documentada). Não hardcodar a lista de entidades — é
/// isso que garante que o teste pega entidade nova no futuro.
/// </summary>
public class TenantFilterCoverageTests
{
    // Entidades com IdClinica intencionalmente FORA do ApplyTenantFilters.
    // Cada entrada exige compensação manual no service — documentar onde.
    private static readonly Dictionary<string, string> CompensadasManualmente = new()
    {
        ["Agendamento"] = "AgendaService.cs:39,53 — filtro manual por IClinicaContext.IdClinica",
    };

    private static KuraDbContext CreateContext(long? idClinicaFiltro, string? dbName = null)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    /// <summary>
    /// Descoberta por reflection — NÃO hardcodar. Qualquer classe pública e
    /// concreta em Kura.Domain.Entities com uma propriedade IdClinica entra
    /// automaticamente na cobertura deste teste.
    /// </summary>
    public static TheoryData<Type> EntidadesComIdClinica()
    {
        var data = new TheoryData<Type>();

        var tipos = typeof(Clinica).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "Kura.Domain.Entities")
            .Where(t => t.GetProperty("IdClinica", BindingFlags.Public | BindingFlags.Instance) != null)
            .OrderBy(t => t.Name);

        foreach (var tipo in tipos)
        {
            data.Add(tipo);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EntidadesComIdClinica))]
    public void TodaEntidadeComIdClinica_EstaNoFiltroOuNaAllowlist(Type entidade)
    {
        if (CompensadasManualmente.ContainsKey(entidade.Name))
        {
            // Compensação manual documentada — intencionalmente fora do filtro.
            return;
        }

        using var ctx = CreateContext(idClinicaFiltro: null);
        var entityType = ctx.Model.FindEntityType(entidade);

        entityType.Should().NotBeNull(
            $"`{entidade.Name}` declara IdClinica mas não foi encontrada no modelo do EF " +
            "(KuraDbContext) — confira se há um DbSet e um mapeamento para ela.");

        // GetQueryFilter() está [Obsolete("Use GetDeclaredQueryFilters() instead.")]
        // no EF Core 10.0.7 (conferido por reflection contra o pacote referenciado
        // no .csproj deste projeto) — usar a API nova evita warning de obsolescência.
        var filtros = entityType!.GetDeclaredQueryFilters();

        filtros.Should().NotBeEmpty(
            $"`{entidade.Name}` declara IdClinica mas não está em ApplyTenantFilters nem na " +
            "allowlist de compensação manual. Registre em " +
            "KuraDbContext.ApplyTenantFilters, ou adicione à allowlist deste teste " +
            "(TenantFilterCoverageTests.CompensadasManualmente) com a referência do " +
            "filtro manual no service.");

        var expressaoTexto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));

        expressaoTexto.Should().Contain("IdClinicaFiltro",
            $"`{entidade.Name}` tem um query filter declarado, mas a expressão não referencia " +
            "IdClinicaFiltro — provavelmente é só o filtro de StAtiva, sem isolamento de tenant. " +
            "Registre o filtro de tenant em KuraDbContext.ApplyTenantFilters, ou adicione à " +
            "allowlist deste teste com a referência do filtro manual no service.");
    }

    [Fact]
    public void Allowlist_ContemApenasAgendamento()
    {
        CompensadasManualmente.Keys.Should().BeEquivalentTo(
            new[] { "Agendamento" },
            "a allowlist de compensação manual deve conter EXATAMENTE as entidades " +
            "documentadas — qualquer entrada nova precisa vir com a referência do filtro " +
            "manual no service (ver comentário da allowlist)");
    }

    [Fact]
    public async Task FiltroDeTenant_DesligaInteiro_QuandoIdClinicaFiltroEhNull()
    {
        // Armadilha conhecida (documentada em CLAUDE.md): o filtro de tenant em
        // ApplyTenantFilters DESLIGA por completo quando IdClinicaFiltro == null
        // (chamada sem contexto de clínica) — ele não nega/filtra para zero linhas,
        // ele simplesmente para de restringir por clínica. Isso é usado de propósito
        // por endpoints cross-tenant (ex.: MetricsController), mas é fácil confundir
        // com "sem clínica = sem dados". Esta task não corrige o comportamento — só o
        // fixa em teste, para que mudar esse predicado no futuro seja uma decisão
        // consciente, não um efeito colateral silencioso.
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.Clinicas.AddRange(
                new Clinica
                {
                    Id = 1,
                    NmClinica = "Clinica A",
                    NrCnpj = "00000000000101",
                    DsEndereco = "Rua A, 1",
                    NmCidade = "Sao Paulo",
                    SgUf = "SP",
                    NrCep = "00000001",
                    DsEmail = "a@teste.com",
                    DsEmailAcesso = "a@teste.com",
                    DsSenhaHash = "hash",
                    StAtiva = true
                },
                new Clinica
                {
                    Id = 2,
                    NmClinica = "Clinica B",
                    NrCnpj = "00000000000102",
                    DsEndereco = "Rua B, 2",
                    NmCidade = "Rio de Janeiro",
                    SgUf = "RJ",
                    NrCep = "00000002",
                    DsEmail = "b@teste.com",
                    DsEmailAcesso = "b@teste.com",
                    DsSenhaHash = "hash",
                    StAtiva = true
                });

            seedCtx.Veterinarios.AddRange(
                new Veterinario
                {
                    Id = 1,
                    IdClinica = 1,
                    NmVeterinario = "Dr. Clinica A",
                    NrCrmv = "SP-000001",
                    DsEmail = "vetA@teste.com",
                    StAtiva = true
                },
                new Veterinario
                {
                    Id = 2,
                    IdClinica = 2,
                    NmVeterinario = "Dr. Clinica B",
                    NrCrmv = "RJ-000002",
                    DsEmail = "vetB@teste.com",
                    StAtiva = true
                });

            await seedCtx.SaveChangesAsync();
        }

        using var ctxClinicaA = CreateContext(idClinicaFiltro: 1, dbName);
        var vetsClinicaA = await ctxClinicaA.Veterinarios.AsNoTracking().ToListAsync();
        vetsClinicaA.Should().ContainSingle()
            .Which.IdClinica.Should().Be(1, "com IdClinicaFiltro = 1, só a clínica 1 deve aparecer");

        using var ctxSemFiltro = CreateContext(idClinicaFiltro: null, dbName);
        var vetsSemFiltro = await ctxSemFiltro.Veterinarios.AsNoTracking().ToListAsync();
        vetsSemFiltro.Should().HaveCount(2,
            "quando IdClinicaFiltro é null o filtro DESLIGA inteiro (não nega para zero " +
            "linhas) — as duas clínicas devem aparecer. Se esta asserção quebrar, o " +
            "predicado de ApplyTenantFilters mudou de comportamento; confirme que é " +
            "intencional antes de ajustar o teste.");
    }
}
