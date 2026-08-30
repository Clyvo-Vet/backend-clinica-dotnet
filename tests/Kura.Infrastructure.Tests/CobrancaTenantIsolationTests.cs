namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-08 (ciclo FIN) — prova de mordida de <c>COBRANCA</c> (V18__financeiro.sql,
/// repo backend-tutor-java): isolamento de tenant, mapeamento coluna a coluna e
/// <b>o tipo e a precisão do dinheiro</b>.
///
/// <para><b>Aqui o filtro de tenant não é defesa em profundidade, é a defesa
/// principal.</b> <c>COBRANCA.ID_CLINICA</c> é denormalizado de
/// <c>EVENTO_CLINICO</c> de propósito (comentário da coluna na V18) justamente para
/// que este predicado exista e para que os KPI da FD-11 agrupem sem join. Uma
/// consulta de receita que escapasse do filtro somaria dinheiro de outra clínica —
/// e a soma não avisa que está errada.</para>
///
/// <para>Provider InMemory. Replicar contra Oracle real é da FD-12; até lá,
/// <see cref="Mapeamento_TravaTipoEPrecisaoDoDinheiro"/> é a única defesa contra
/// arredondamento silencioso de dinheiro.</para>
/// </summary>
public class CobrancaTenantIsolationTests
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

    private static Clinica NovaClinica(long id, string sufixo) => new()
    {
        Id = id,
        NmClinica = $"Clinica {sufixo}",
        NrCnpj = $"0000000000010{id}",
        DsEndereco = $"Rua {sufixo}, {id}",
        NmCidade = "Sao Paulo",
        SgUf = "SP",
        NrCep = $"0000000{id}",
        DsEmail = $"{sufixo}@teste.com",
        DsEmailAcesso = $"{sufixo}@teste.com",
        DsSenhaHash = "hash",
        StAtiva = true
    };

    /// <summary>
    /// Duas clínicas, um atendimento cada, duas cobranças cada. A cadeia
    /// <c>EVENTO_CLINICO</c> é montada de verdade (pet, veterinário, tipo de evento)
    /// porque a V18 pendura a cobrança num atendimento que aconteceu (D-3) — cobrança
    /// apontando um <c>ID_EVENTO_CLINICO</c> inventado seria uma fixture que o Oracle
    /// recusaria.
    ///
    /// <para>⚠️ <c>TIPO_EVENTO.CD_TIPO</c> é NOT NULL + UNIQUE desde a V9 (armadilha de
    /// fixture herdada da FD-07): os dois tipos abaixo preenchem <c>CdTipo</c> e com
    /// códigos distintos.</para>
    /// </summary>
    private static async Task SeedDuasClinicasAsync(string dbName)
    {
        using var seedCtx = CreateContext(idClinicaFiltro: null, dbName);

        seedCtx.Clinicas.AddRange(NovaClinica(1, "A"), NovaClinica(2, "B"));

        seedCtx.Veterinarios.AddRange(
            new Veterinario { Id = 1, IdClinica = 1, NmVeterinario = "Dr. A", NrCrmv = "SP-000001", DsEmail = "vetA@teste.com", StAtiva = true },
            new Veterinario { Id = 2, IdClinica = 2, NmVeterinario = "Dr. B", NrCrmv = "RJ-000002", DsEmail = "vetB@teste.com", StAtiva = true });

        seedCtx.Pets.AddRange(
            new Pet { Id = 1, IdClinica = 1, IdEspecie = 1, IdRaca = 1, NmPet = "Rex", DtNascimento = new DateTime(2020, 1, 1), SgSexo = 'M', SgPorte = 'M', StAtiva = true },
            new Pet { Id = 2, IdClinica = 2, IdEspecie = 1, IdRaca = 1, NmPet = "Mel", DtNascimento = new DateTime(2021, 1, 1), SgSexo = 'F', SgPorte = 'P', StAtiva = true });

        seedCtx.TiposEvento.AddRange(
            new TipoEvento { Id = 1, CdTipo = "CONSULTA", NmTipo = "Consulta", StAtiva = true },
            new TipoEvento { Id = 2, CdTipo = "VACINA", NmTipo = "Vacina", StAtiva = true });

        seedCtx.EventosClinicos.AddRange(
            new EventoClinico { Id = 1, IdClinica = 1, IdPet = 1, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = new DateTime(2026, 8, 20), DsObservacao = "Atendimento A", StAtiva = true },
            new EventoClinico { Id = 2, IdClinica = 2, IdPet = 2, IdVeterinario = 2, IdTipoEvento = 1, DtEvento = new DateTime(2026, 8, 21), DsObservacao = "Atendimento B", StAtiva = true });

        seedCtx.ServicosPreco.AddRange(
            new ServicoPreco { Id = 1, IdClinica = 1, NmServico = "Consulta (A)", VlPreco = 180.50m, StAtiva = true },
            new ServicoPreco { Id = 2, IdClinica = 2, NmServico = "Consulta (B)", VlPreco = 240.00m, StAtiva = true });

        seedCtx.Cobrancas.AddRange(
            new Cobranca
            {
                Id = 1,
                IdEventoClinico = 1,
                IdClinica = 1,
                IdServicoPreco = 1,
                VlCobrado = 180.50m,
                DsFormaPagamento = "PIX",
                DtCobranca = new DateTime(2026, 8, 20),
                StAtiva = true
            },
            // Lançamento avulso: ID_SERVICO_PRECO nulo é legítimo (D-2).
            new Cobranca
            {
                Id = 2,
                IdEventoClinico = 1,
                IdClinica = 1,
                IdServicoPreco = null,
                VlCobrado = 19.99m,
                DsFormaPagamento = null,
                DtCobranca = new DateTime(2026, 8, 20),
                StAtiva = true
            },
            new Cobranca
            {
                Id = 3,
                IdEventoClinico = 2,
                IdClinica = 2,
                IdServicoPreco = 2,
                VlCobrado = 240.00m,
                DsFormaPagamento = "CREDITO",
                DtCobranca = new DateTime(2026, 8, 21),
                StAtiva = true
            },
            new Cobranca
            {
                Id = 4,
                IdEventoClinico = 2,
                IdClinica = 2,
                IdServicoPreco = null,
                VlCobrado = 60.00m,
                DsFormaPagamento = "DINHEIRO",
                DtCobranca = new DateTime(2026, 8, 21),
                StAtiva = true
            });

        await seedCtx.SaveChangesAsync();
    }

    [Fact]
    public async Task ComContextoDaClinicaA_SoAsCobrancasDaClinicaA_Voltam()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var cobrancas = await ctx.Cobrancas.AsNoTracking().ToListAsync();

        // Assert — a mordida: sem a entrada em ApplyTenantFilters isto devolve 4.
        cobrancas.Should().HaveCount(2,
            "com IdClinicaFiltro = 1 apenas as 2 cobranças da clínica A podem voltar");
        cobrancas.Should().OnlyContain(c => c.IdClinica == 1);

        // A asserção que descreve o dano real: a soma. Um vazamento aqui não devolve
        // "uma linha estranha na tela" — devolve receita bruta errada (FD-11).
        cobrancas.Sum(c => c.VlCobrado).Should().Be(200.49m,
            "receita da clínica A é 180,50 + 19,99 = 200,49. Com o filtro fora, esta " +
            "soma passaria a 500,49 — dinheiro das duas clínicas somado, sem erro nenhum");
    }

    [Fact]
    public async Task ComContextoDaClinicaB_SoAsCobrancasDaClinicaB_Voltam()
    {
        // Arrange — o espelho do teste acima. Sem ele, um filtro com a clínica
        // hardcodada em 1 passaria despercebido.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 2, dbName);
        var cobrancas = await ctx.Cobrancas.AsNoTracking().ToListAsync();

        // Assert
        cobrancas.Should().HaveCount(2);
        cobrancas.Should().OnlyContain(c => c.IdClinica == 2);
        cobrancas.Sum(c => c.VlCobrado).Should().Be(300.00m);
    }

    [Fact]
    public async Task SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas()
    {
        // Arrange — controle positivo do par acima: prova que as 4 linhas ESTÃO no
        // banco e que "HaveCount(2)" mede filtragem, não seed vazio. E fixa a
        // armadilha documentada: com IdClinicaFiltro null o filtro DESLIGA, não nega.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var cobrancas = await ctx.Cobrancas.AsNoTracking().ToListAsync();

        // Assert
        cobrancas.Should().HaveCount(4);
        cobrancas.Sum(c => c.VlCobrado).Should().Be(500.49m,
            "sem contexto de clínica o filtro desliga inteiro — este é exatamente o " +
            "número errado que um KPI veria se alguém consultasse receita fora de um " +
            "JWT de clínica. A FD-11 precisa escopar explicitamente.");
    }

    [Fact]
    public async Task CobrancaAvulsa_ComIdServicoPrecoNulo_SobreviveAoFiltro()
    {
        // Arrange — ID_SERVICO_PRECO nullable é decisão da V18 (D-2): valor avulso sem
        // serviço tabelado é lançamento legítimo. Se o mapeamento inferisse NOT NULL,
        // ou se algum filtro derrubasse a linha, essa receita sumiria em silêncio.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var avulsa = await ctx.Cobrancas.AsNoTracking()
            .SingleAsync(c => c.IdServicoPreco == null);

        // Assert
        avulsa.VlCobrado.Should().Be(19.99m);
        avulsa.DsFormaPagamento.Should().BeNull(
            "DS_FORMA_PAGAMENTO é nullable na V18 — exigi-la forçaria o veterinário a " +
            "preenchê-la no meio do atendimento");
        avulsa.IdClinica.Should().Be(1);
    }

    [Fact]
    public async Task StAtivaFalse_NuncaAparece_MesmoComContextoDaPropriaClinica()
    {
        // Arrange — soft delete no padrão do projeto. Cobrança "removida" é cobrança
        // que sai do faturamento; se a metade StAtiva do predicado sumir, ela volta a
        // ser somada.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.Cobrancas.Add(new Cobranca
            {
                Id = 5,
                IdEventoClinico = 1,
                IdClinica = 1,
                IdServicoPreco = null,
                VlCobrado = 999.99m,
                DtCobranca = new DateTime(2026, 8, 20),
                StAtiva = false
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var cobrancas = await ctx.Cobrancas.AsNoTracking().ToListAsync();

        // Assert
        cobrancas.Should().HaveCount(2);
        cobrancas.Sum(c => c.VlCobrado).Should().Be(200.49m,
            "a cobrança inativa de 999,99 não pode entrar na receita");

        // Controle positivo do soft delete: a linha EXISTE (o filtro é quem a esconde).
        using var ctxSemFiltro = CreateContext(idClinicaFiltro: null, dbName);
        var todas = await ctxSemFiltro.Cobrancas.IgnoreQueryFilters()
            .AsNoTracking().ToListAsync();
        todas.Should().HaveCount(5);
        todas.Should().Contain(c => c.VlCobrado == 999.99m);
    }

    [Fact]
    public void Mapeamento_BateComOSchemaDaV18_NomeDeTabelaEColunas()
    {
        // Arrange — um typo de nome de coluna não falha nada sob InMemory (que ignora
        // nomes de coluna); falha contra o Oracle com ORA-00904, muito depois.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var entityType = ctx.Model.FindEntityType(typeof(Cobranca));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("COBRANCA");

        entityType.GetProperties().Select(p => p.GetColumnName()).Should().BeEquivalentTo(new[]
        {
            "ID_COBRANCA",
            "ID_EVENTO_CLINICO",
            "ID_CLINICA",
            "ID_SERVICO_PRECO",
            "VL_COBRADO",
            "DS_FORMA_PAGAMENTO",
            "DT_COBRANCA",
            "ST_ATIVA",
            "DT_CRIACAO",
            "DT_ATUALIZACAO"
        }, "as colunas devem bater EXATAMENTE com V18__financeiro.sql — nem a mais, nem a menos");

        entityType.FindProperty(nameof(Cobranca.IdClinica))!.IsNullable
            .Should().BeFalse("ID_CLINICA é NOT NULL na V18 — é a chave do isolamento e do KPI");
        entityType.FindProperty(nameof(Cobranca.IdEventoClinico))!.IsNullable
            .Should().BeFalse("não existe lançamento sem atendimento (D-3)");
        entityType.FindProperty(nameof(Cobranca.IdServicoPreco))!.IsNullable
            .Should().BeTrue("valor avulso sem serviço tabelado é lançamento legítimo (D-2)");
        entityType.FindProperty(nameof(Cobranca.DsFormaPagamento))!.IsNullable
            .Should().BeTrue("DS_FORMA_PAGAMENTO é nullable e sem CHECK na V18");
        entityType.FindProperty(nameof(Cobranca.DsFormaPagamento))!.GetMaxLength()
            .Should().Be(30, "DS_FORMA_PAGAMENTO é VARCHAR2(30) na V18");
        entityType.FindProperty(nameof(Cobranca.DtCobranca))!.IsNullable
            .Should().BeFalse("linha com data nula seria invisível a todo KPI por período");

        entityType.FindProperty(nameof(Cobranca.Id))!.GetDefaultValueSql()
            .Should().Be("SEQ_COBRANCA.NEXTVAL",
                "a V18 declara DEFAULT SEQ_COBRANCA.NEXTVAL na PK — nome divergente vira " +
                "ORA-02289 no primeiro INSERT contra Oracle, e o InMemory nunca lê isto");
    }

    [Fact]
    public void Mapeamento_TravaTipoEPrecisaoDoDinheiro()
    {
        // Arrange
        // 🔴 O teste central desta task, na coluna que mais dói. VL_COBRADO é
        // NUMBER(10,2) na V18 e é o que a FD-11 SOMA para produzir receita bruta e
        // ticket médio: erro de centavo por linha vira erro de relatório agregado.
        //
        // O provider InMemory NÃO reprova nada disto — a suíte fica verde com `double`,
        // com HasPrecision(10, 0) e sem HasPrecision nenhum. O modo de falha, medido na
        // FD-07 do lado Java: NUMBER(10) faz 999.99 virar 1000 EM SILÊNCIO.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var vlCobrado = ctx.Model.FindEntityType(typeof(Cobranca))!
            .FindProperty(nameof(Cobranca.VlCobrado))!;

        // Assert
        vlCobrado.ClrType.Should().Be(typeof(decimal),
            "dinheiro em `double`/`float` não representa centavos exatamente; a soma de " +
            "uma receita acumula erro e o round-trip com o Oracle deixa de ser idêntico");

        vlCobrado.GetPrecision().Should().Be(10,
            "VL_COBRADO é NUMBER(10,2) na V18 — precisão declarada, não inferida");

        vlCobrado.GetScale().Should().Be(2,
            "escala 2 é o que guarda os CENTAVOS; com escala 0 o banco arredonda em " +
            "silêncio (999.99 → 1000, medido na FD-07) — nenhuma exceção, nenhum log");

        vlCobrado.GetColumnType().Should().Be("NUMBER(10,2)",
            "o tipo de coluna declarado tem que ser literalmente o da V18");
    }

    [Fact]
    public async Task SomaDeCentavos_NaoAcumulaErro_ETerceiraCasaNaoApareceDoNada()
    {
        // Arrange — complemento comportamental do teste de metadado. Sob InMemory isto
        // NÃO prova o banco (nenhuma coerção acontece); prova o tipo CLR: em `double`,
        // 0.1 + 0.2 != 0.3 e a soma de 10 parcelas de 0,07 não fecha em 0,70.
        // A prova contra o Oracle (round-trip e arredondamento na terceira casa) é da
        // FD-12, item 2 do backlog.
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "A"));
            for (var i = 1; i <= 10; i++)
            {
                seedCtx.Cobrancas.Add(new Cobranca
                {
                    Id = i,
                    IdEventoClinico = 1,
                    IdClinica = 1,
                    VlCobrado = 0.07m,
                    DtCobranca = new DateTime(2026, 8, 20),
                    StAtiva = true
                });
            }
            await seedCtx.SaveChangesAsync();
        }

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var cobrancas = await ctx.Cobrancas.AsNoTracking().ToListAsync();

        // Assert
        cobrancas.Sum(c => c.VlCobrado).Should().Be(0.70m,
            "dez parcelas de sete centavos são exatamente setenta centavos em decimal; " +
            "em double a mesma soma dá 0.7000000000000001");
    }

    [Fact]
    public void QueryFilter_DoModeloCompleto_TemTenantESoftDelete_EmUmFiltroSo()
    {
        // Arrange — modelo COMPLETO (configuração + ApplyTenantFilters). Não é capaz de
        // denunciar filtro anônimo duplicado na configuração — essa guarda é a de baixo.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(Cobranca))!.GetDeclaredQueryFilters();

        // Assert
        filtros.Should().HaveCount(1,
            "o isolamento desta entidade tem que ser legível num predicado só");

        var texto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));
        texto.Should().Contain("IdClinicaFiltro", "o filtro precisa isolar por tenant");
        texto.Should().Contain("StAtiva", "e precisa manter o soft delete");
    }

    [Fact]
    public void Configuracao_NaoDeclaraQueryFilterProprio()
    {
        // Arrange — guarda replicada da FD-02 (achado F1 da revisão G2). Medido em EF
        // Core 10.0.7: dois filtros ANÔNIMOS não se combinam nem lançam — o segundo
        // SUBSTITUI o primeiro em silêncio. Como ApplyConfigurationsFromAssembly roda
        // ANTES de ApplyTenantFilters, um filtro anônimo em CobrancaConfiguration é
        // apagado pelo do contexto e fica INVISÍVEL no modelo completo.
        using var ctx = new ContextoSoComAConfiguracao();

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(Cobranca))!.GetDeclaredQueryFilters();

        // Assert
        var texto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));
        filtros.Should().BeEmpty(
            "CobrancaConfiguration não pode declarar HasQueryFilter: o filtro desta " +
            "entidade vive só em KuraDbContext.ApplyTenantFilters. Um filtro anônimo aqui " +
            "seria silenciosamente substituído por aquele (a configuração roda antes) e " +
            "viraria código morto; um filtro NOMEADO aqui derruba o build do modelo com " +
            "\"Both anonymous and named query filters cannot be applied simultaneously\". " +
            $"Filtro(s) encontrado(s): {texto}");
    }

    /// <summary>
    /// Contexto mínimo que aplica <b>apenas</b> <see cref="CobrancaConfiguration"/> —
    /// sem <c>ApplyConfigurationsFromAssembly</c> e, sobretudo, sem
    /// <c>ApplyTenantFilters</c>.
    /// </summary>
    private sealed class ContextoSoComAConfiguracao : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase($"config-only-cobranca-{Guid.NewGuid()}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new CobrancaConfiguration());
    }
}
