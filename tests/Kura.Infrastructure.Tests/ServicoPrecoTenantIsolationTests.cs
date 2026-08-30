namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-08 (ciclo FIN) — prova de mordida de <c>SERVICO_PRECO</c>
/// (V18__financeiro.sql, repo backend-tutor-java): isolamento de tenant, mapeamento
/// coluna a coluna e, sobretudo, <b>o tipo e a precisão do dinheiro</b>.
///
/// <para><b>Por que um teste próprio, se TenantFilterCoverageTests já existe:</b>
/// aquele é de COBERTURA — prova que a entidade tem ALGUM query filter cuja expressão
/// menciona <c>IdClinicaFiltro</c>. Ele não executa nenhuma consulta com dado de duas
/// clínicas, então um predicado sintaticamente válido e semanticamente errado passaria
/// por ele. Aqui a asserção é sobre o RESULTADO da consulta.</para>
///
/// <para>Provider InMemory, no padrão de <c>UsuarioClinicaTenantIsolationTests</c>.
/// Replicar contra Oracle real é responsabilidade da FD-12 — e é exatamente por isso
/// que <see cref="Mapeamento_TravaTipoEPrecisaoDoDinheiro"/> existe: até a FD-12, ele
/// é a ÚNICA coisa entre um <c>double</c> (ou uma precisão errada) e a produção.</para>
/// </summary>
public class ServicoPrecoTenantIsolationTests
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
    /// Duas clínicas, dois itens de catálogo cada. Os valores carregam centavos de
    /// propósito — preço redondo esconderia truncamento de escala.
    /// </summary>
    private static async Task SeedDuasClinicasAsync(string dbName)
    {
        using var seedCtx = CreateContext(idClinicaFiltro: null, dbName);

        seedCtx.Clinicas.AddRange(NovaClinica(1, "A"), NovaClinica(2, "B"));

        seedCtx.ServicosPreco.AddRange(
            new ServicoPreco
            {
                Id = 1,
                IdClinica = 1,
                NmServico = "Consulta clinica geral (A)",
                VlPreco = 180.50m,
                StAtiva = true
            },
            new ServicoPreco
            {
                Id = 2,
                IdClinica = 1,
                NmServico = "Vacina antirrabica (A)",
                VlPreco = 79.99m,
                StAtiva = true
            },
            new ServicoPreco
            {
                Id = 3,
                IdClinica = 2,
                NmServico = "Consulta clinica geral (B)",
                VlPreco = 240.00m,
                StAtiva = true
            },
            new ServicoPreco
            {
                Id = 4,
                IdClinica = 2,
                NmServico = "Banho e tosa (B)",
                VlPreco = 95.90m,
                StAtiva = true
            });

        await seedCtx.SaveChangesAsync();
    }

    [Fact]
    public async Task ComContextoDaClinicaA_SoOsPrecosDaClinicaA_Voltam()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var precos = await ctx.ServicosPreco.AsNoTracking().ToListAsync();

        // Assert — a mordida: sem a entrada em ApplyTenantFilters isto devolve 4.
        precos.Should().HaveCount(2,
            "com IdClinicaFiltro = 1 apenas os 2 itens de catálogo da clínica A podem voltar");
        precos.Should().OnlyContain(p => p.IdClinica == 1);
        precos.Should().NotContain(p => p.NmServico.Contains("(B)"),
            "tabela de preço é informação comercial da clínica — o preço do concorrente " +
            "jamais pode atravessar o filtro");
    }

    [Fact]
    public async Task ComContextoDaClinicaB_SoOsPrecosDaClinicaB_Voltam()
    {
        // Arrange — o espelho do teste acima. Sem ele, um filtro escrito com a clínica
        // hardcodada em 1 passaria despercebido.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 2, dbName);
        var precos = await ctx.ServicosPreco.AsNoTracking().ToListAsync();

        // Assert
        precos.Should().HaveCount(2);
        precos.Should().OnlyContain(p => p.IdClinica == 2);
    }

    [Fact]
    public async Task SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas()
    {
        // Arrange — armadilha documentada (CLAUDE.md / TenantFilterCoverageTests): com
        // IdClinicaFiltro null o filtro DESLIGA; ele não nega para zero linhas. Este é
        // o controle positivo do par acima: ele prova que as 4 linhas ESTÃO no banco, e
        // portanto que o "HaveCount(2)" mede filtragem, não seed vazio.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var precos = await ctx.ServicosPreco.AsNoTracking().ToListAsync();

        // Assert
        precos.Should().HaveCount(4,
            "sem contexto de clínica o filtro desliga inteiro — comportamento " +
            "documentado, não bug. A FD-09 precisa escopar explicitamente qualquer " +
            "leitura feita fora de um JWT de clínica.");
    }

    [Fact]
    public async Task StAtivaFalse_NuncaAparece_MesmoComContextoDaPropriaClinica()
    {
        // Arrange — soft delete no padrão do projeto. Serviço desativado é serviço que
        // saiu da tabela de preços; se a metade StAtiva do predicado sumir, ele volta a
        // ser ofertável.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.ServicosPreco.Add(new ServicoPreco
            {
                Id = 5,
                IdClinica = 1,
                NmServico = "Servico descontinuado (A)",
                VlPreco = 10.00m,
                StAtiva = false
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var precos = await ctx.ServicosPreco.AsNoTracking().ToListAsync();

        // Assert
        precos.Should().HaveCount(2);
        precos.Should().NotContain(p => p.NmServico.Contains("descontinuado"));

        // Controle positivo do próprio soft delete: a linha desativada EXISTE no banco
        // (o filtro é quem a esconde), senão esta asserção mediria um seed que falhou.
        using var ctxSemFiltro = CreateContext(idClinicaFiltro: null, dbName);
        var todas = await ctxSemFiltro.ServicosPreco.IgnoreQueryFilters()
            .AsNoTracking().ToListAsync();
        todas.Should().HaveCount(5);
        todas.Should().Contain(p => p.NmServico.Contains("descontinuado"));
    }

    [Fact]
    public void Mapeamento_BateComOSchemaDaV18_NomeDeTabelaEColunas()
    {
        // Arrange — um typo de nome de coluna não falha nenhum teste de comportamento
        // sob InMemory (que ignora nomes de coluna); falha contra o Oracle com
        // ORA-00904, muito depois. Este teste fecha essa distância.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var entityType = ctx.Model.FindEntityType(typeof(ServicoPreco));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("SERVICO_PRECO");

        entityType.GetProperties().Select(p => p.GetColumnName()).Should().BeEquivalentTo(new[]
        {
            "ID_SERVICO_PRECO",
            "ID_CLINICA",
            "NM_SERVICO",
            "VL_PRECO",
            "ST_ATIVA",
            "DT_CRIACAO",
            "DT_ATUALIZACAO"
        }, "as colunas devem bater EXATAMENTE com V18__financeiro.sql — nem a mais, nem a menos");

        entityType.FindProperty(nameof(ServicoPreco.IdClinica))!.IsNullable
            .Should().BeFalse("ID_CLINICA é NOT NULL na V18 — tabela de preço é sempre de uma clínica");
        entityType.FindProperty(nameof(ServicoPreco.NmServico))!.GetMaxLength()
            .Should().Be(200, "NM_SERVICO é VARCHAR2(200) na V18");
        entityType.FindProperty(nameof(ServicoPreco.VlPreco))!.IsNullable
            .Should().BeFalse("VL_PRECO é NOT NULL na V18");

        // Mesma lacuna medida na FD-02 (achado F3): trocar o nome da sequence deixava a
        // suíte inteira verde, porque o InMemory gera as PKs sozinho e nunca lê esta
        // expressão. Quem quebra é o Oracle, com ORA-02289, em runtime.
        entityType.FindProperty(nameof(ServicoPreco.Id))!.GetDefaultValueSql()
            .Should().Be("SEQ_SERVICO_PRECO.NEXTVAL",
                "a V18 declara DEFAULT SEQ_SERVICO_PRECO.NEXTVAL na PK");
    }

    [Fact]
    public void Mapeamento_TravaTipoEPrecisaoDoDinheiro()
    {
        // Arrange
        // 🔴 O teste central desta task. VL_PRECO é NUMBER(10,2) na V18.
        //
        // Por que ele precisa existir: o provider InMemory NÃO reprova nada disto. A
        // suíte fica verde com `double` no lugar de `decimal`, com HasPrecision(10, 0),
        // e sem HasPrecision nenhum — porque InMemory guarda o objeto CLR como está,
        // sem nenhuma coerção de banco. Só a FD-12 (Oracle real) mediria o efeito.
        //
        // O modo de falha, medido na FD-07 do lado Java: NUMBER(10) no lugar de
        // NUMBER(10,2) faz 999.99 virar 1000 EM SILÊNCIO. Dinheiro não estoura
        // exceção quando é arredondado — ele simplesmente fica errado, e o erro só
        // aparece num relatório de receita meses depois.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var vlPreco = ctx.Model.FindEntityType(typeof(ServicoPreco))!
            .FindProperty(nameof(ServicoPreco.VlPreco))!;

        // Assert
        vlPreco.ClrType.Should().Be(typeof(decimal),
            "dinheiro em `double`/`float` não representa centavos exatamente (0,1 e 0,07 " +
            "não têm representação finita em base 2): a soma acumula erro e o round-trip " +
            "com o Oracle deixa de ser idêntico");

        vlPreco.GetPrecision().Should().Be(10,
            "VL_PRECO é NUMBER(10,2) na V18 — precisão declarada, não inferida pelo provider");

        vlPreco.GetScale().Should().Be(2,
            "escala 2 é o que guarda os CENTAVOS. Com escala 0 o banco arredonda em " +
            "silêncio (999.99 → 1000, medido na FD-07) — nenhuma exceção, nenhum log");

        // GetColumnType() NÃO pode ser usado aqui: sob o provider InMemory ele
        // lança InvalidCastException (InMemoryTypeMapping não é RelationalTypeMapping) —
        // medido nesta task, na primeira execução. A anotação crua guarda exatamente o
        // que HasColumnType declarou, e é legível em qualquer provider.
        // F1 (fix wave pós-G2) — ACHADO DA REVISÃO, e ele é do tipo que este projeto
        // mais repete: a versão anterior desta asserção era
        //     vlPreco.FindAnnotation("Relational:ColumnType")?.Value.Should().Be(...)
        // e o `?.` CURTO-CIRCUITA A CADEIA INTEIRA: com a anotação ausente, `.Should()`
        // nunca executa e o teste passa VERDE. Medido pela G2 removendo o HasColumnType
        // de vlPreco: a suíte ficou 10/10 verde. A mutação original desta task cobria
        // "valor errado", nunca "declaração ausente" — que é o caso realista.
        //
        // ⚠️ Regra geral que sai daqui: `?.` antes de `.Should()` DESARMA o
        // FluentAssertions em silêncio. Separar a busca da asserção é o que garante que
        // as DUAS falhas (ausente e divergente) mordam, cada uma com mensagem própria.
        var anotacaoTipoColuna = vlPreco.FindAnnotation("Relational:ColumnType");

        anotacaoTipoColuna.Should().NotBeNull(
            "VL_PRECO tem que declarar HasColumnType explicitamente: sem ele o provider " +
            "Oracle escolhe o tipo por default do mapeamento e o modelo passa a não afirmar " +
            "NADA sobre a coluna de dinheiro");

        anotacaoTipoColuna!.Value.Should().Be("NUMBER(10,2)",
            "o tipo de coluna declarado tem que ser literalmente o da V18 (VL_PRECO " +
            "NUMBER(10,2)); divergência EF↔Flyway numa tabela nova é dívida criada de graça");
    }

    [Fact]
    public async Task ValorComCentavos_SobreviveAoRoundTrip_SemArredondar()
    {
        // Arrange — complemento comportamental do teste de metadado acima. Sob
        // InMemory isto NÃO prova o banco (nenhuma coerção acontece); prova o tipo CLR:
        // com `double` no lugar de `decimal`, 1234.56 e 0.07 deixam de comparar iguais.
        // A prova contra o Oracle é da FD-12, item 2 do backlog.
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "A"));
            seedCtx.ServicosPreco.Add(new ServicoPreco
            {
                Id = 1,
                IdClinica = 1,
                NmServico = "Servico com centavos",
                VlPreco = 1234.56m,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var preco = await ctx.ServicosPreco.AsNoTracking().SingleAsync();

        // Assert
        preco.VlPreco.Should().Be(1234.56m);
        (preco.VlPreco * 3).Should().Be(3703.68m,
            "três vezes 1234,56 é exatamente 3703,68 em decimal; em ponto flutuante " +
            "binário a igualdade exata não se sustenta");
    }

    [Fact]
    public void QueryFilter_DoModeloCompleto_TemTenantESoftDelete_EmUmFiltroSo()
    {
        // Arrange — olha o modelo COMPLETO (configuração + ApplyTenantFilters). Prova o
        // que o filtro efetivo faz, e NÃO é capaz de denunciar um filtro anônimo
        // duplicado na configuração (esse já teria sido substituído antes de chegar
        // aqui) — essa outra guarda é Configuracao_NaoDeclaraQueryFilterProprio.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(ServicoPreco))!.GetDeclaredQueryFilters();

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
        // Arrange
        // Guarda replicada da FD-02 (achado F1 da revisão G2). Medido em EF Core
        // 10.0.7: dois filtros ANÔNIMOS para a mesma entidade não se combinam nem
        // lançam — o segundo SUBSTITUI o primeiro em silêncio. Como
        // ApplyConfigurationsFromAssembly roda ANTES de ApplyTenantFilters, um filtro
        // anônimo declarado em ServicoPrecoConfiguration é apagado pelo do contexto e
        // fica INVISÍVEL em qualquer inspeção do modelo completo.
        //
        // A única forma de enxergá-lo é montar um modelo com a configuração SOZINHA.
        using var ctx = new ContextoSoComAConfiguracao();

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(ServicoPreco))!.GetDeclaredQueryFilters();

        // Assert
        var texto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));
        filtros.Should().BeEmpty(
            "ServicoPrecoConfiguration não pode declarar HasQueryFilter: o filtro desta " +
            "entidade vive só em KuraDbContext.ApplyTenantFilters. Um filtro anônimo aqui " +
            "seria silenciosamente substituído por aquele (a configuração roda antes) e " +
            "viraria código morto; um filtro NOMEADO aqui derruba o build do modelo com " +
            "\"Both anonymous and named query filters cannot be applied simultaneously\". " +
            $"Filtro(s) encontrado(s): {texto}");
    }

    /// <summary>
    /// Contexto mínimo que aplica <b>apenas</b> <see cref="ServicoPrecoConfiguration"/>
    /// — sem <c>ApplyConfigurationsFromAssembly</c> e, sobretudo, sem
    /// <c>ApplyTenantFilters</c>.
    /// </summary>
    private sealed class ContextoSoComAConfiguracao : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase($"config-only-servico-preco-{Guid.NewGuid()}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new ServicoPrecoConfiguration());
    }
}
