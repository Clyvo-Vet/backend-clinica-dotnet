namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-02 (ciclo FIN) — prova de mordida do isolamento de tenant de
/// <c>USUARIO_CLINICA</c>, a tabela que introduz identidade individual no lado
/// clínico (V17__usuario_clinica.sql, repo backend-tutor-java).
///
/// <para><b>Por que um teste de isolamento próprio, se TenantFilterCoverageTests já
/// existe:</b> aquele teste é de COBERTURA — ele prova que a entidade tem ALGUM
/// query filter cuja expressão menciona <c>IdClinicaFiltro</c>. Ele não executa
/// nenhuma consulta com dado de duas clínicas, então um predicado sintaticamente
/// válido e semanticamente errado passaria por ele. Aqui a asserção é sobre o
/// RESULTADO da consulta.</para>
///
/// <para><b>Controle positivo (medido, não alegado):</b> removendo a entrada de
/// <c>UsuarioClinica</c> de <c>KuraDbContext.ApplyTenantFilters</c>, este teste
/// falha — o registro completo da saída literal dessa mutação está em
/// <c>.superpowers/sdd/KURA_BACKLOG_FIN/fd-02-report.md</c>. Um teste de
/// isolamento que passa com e sem o filtro não prova nada.</para>
///
/// <para>Provider InMemory, no padrão de InteracaoCanalTenantIsolationTests —
/// replicar contra Oracle real é responsabilidade do gate de contrato do ciclo,
/// não desta suíte.</para>
/// </summary>
public class UsuarioClinicaTenantIsolationTests
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
    /// Duas clínicas, dois usuários cada. Um dos da clínica A é GESTOR sem
    /// <c>ID_VETERINARIO</c> — o caso que a V17 tornou possível de propósito.
    /// </summary>
    private static async Task SeedDuasClinicasAsync(string dbName)
    {
        using var seedCtx = CreateContext(idClinicaFiltro: null, dbName);

        seedCtx.Clinicas.AddRange(NovaClinica(1, "A"), NovaClinica(2, "B"));

        seedCtx.UsuariosClinica.AddRange(
            new UsuarioClinica
            {
                Id = 1,
                IdClinica = 1,
                IdVeterinario = null,
                DsEmail = "gestor@clinica-a.com",
                DsSenhaHash = "$2a$11$hashDeGestorDaClinicaA",
                TpPerfil = PerfisUsuarioClinica.Gestor,
                StAtiva = true
            },
            new UsuarioClinica
            {
                Id = 2,
                IdClinica = 1,
                IdVeterinario = 10,
                DsEmail = "vet@clinica-a.com",
                DsSenhaHash = "$2a$11$hashDeVetDaClinicaA",
                TpPerfil = PerfisUsuarioClinica.Veterinario,
                StAtiva = true
            },
            new UsuarioClinica
            {
                Id = 3,
                IdClinica = 2,
                IdVeterinario = null,
                DsEmail = "gestor@clinica-b.com",
                DsSenhaHash = "$2a$11$hashDeGestorDaClinicaB",
                TpPerfil = PerfisUsuarioClinica.Gestor,
                StAtiva = true
            },
            new UsuarioClinica
            {
                Id = 4,
                IdClinica = 2,
                IdVeterinario = 20,
                DsEmail = "vet@clinica-b.com",
                DsSenhaHash = "$2a$11$hashDeVetDaClinicaB",
                TpPerfil = PerfisUsuarioClinica.Veterinario,
                StAtiva = true
            });

        await seedCtx.SaveChangesAsync();
    }

    [Fact]
    public async Task ComContextoDaClinicaA_SoOsUsuariosDaClinicaA_Voltam()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var usuarios = await ctx.UsuariosClinica.AsNoTracking().ToListAsync();

        // Assert — a mordida: sem a entrada em ApplyTenantFilters isto devolve 4.
        usuarios.Should().HaveCount(2,
            "com IdClinicaFiltro = 1 apenas os 2 usuários da clínica A podem voltar");
        usuarios.Should().OnlyContain(u => u.IdClinica == 1);
        usuarios.Select(u => u.DsEmail).Should().BeEquivalentTo(
            new[] { "gestor@clinica-a.com", "vet@clinica-a.com" });
        usuarios.Should().NotContain(u => u.DsEmail.Contains("clinica-b"),
            "credencial de outra clínica jamais pode atravessar o filtro — é hash de senha");
    }

    [Fact]
    public async Task ComContextoDaClinicaB_SoOsUsuariosDaClinicaB_Voltam()
    {
        // Arrange — o espelho do teste acima. Sem ele, um filtro escrito com a
        // clínica hardcodada em 1 passaria despercebido.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 2, dbName);
        var usuarios = await ctx.UsuariosClinica.AsNoTracking().ToListAsync();

        // Assert
        usuarios.Should().HaveCount(2);
        usuarios.Should().OnlyContain(u => u.IdClinica == 2);
    }

    [Fact]
    public async Task GestorSemVeterinarioVinculado_SobreviveAoFiltro_ComIdVeterinarioNulo()
    {
        // Arrange — ID_VETERINARIO nullable é a decisão central da V17 (gestor que não
        // atende). Se o mapeamento inferisse NOT NULL, ou se o filtro derrubasse a
        // linha, o perfil GESTOR sumiria em silêncio — e é justamente ele quem a FD-03
        // vai autenticar em ambiente novo.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var gestor = await ctx.UsuariosClinica.AsNoTracking()
            .SingleAsync(u => u.TpPerfil == PerfisUsuarioClinica.Gestor);

        // Assert
        gestor.IdVeterinario.Should().BeNull();
        gestor.IdClinica.Should().Be(1);
    }

    [Fact]
    public async Task SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas()
    {
        // Arrange
        // Armadilha documentada (CLAUDE.md / TenantFilterCoverageTests): com
        // IdClinicaFiltro null o filtro DESLIGA — ele não nega para zero linhas.
        // Fixar isso em teste importa especialmente aqui: o login da FD-03 lê esta
        // tabela ANTES de existir clínica no contexto, então ele NÃO pode contar com
        // este filtro para escopar nada.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        // Act
        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var usuarios = await ctx.UsuariosClinica.AsNoTracking().ToListAsync();

        // Assert
        usuarios.Should().HaveCount(4,
            "sem contexto de clínica o filtro desliga inteiro — comportamento " +
            "documentado, não bug. A FD-03 precisa escopar a busca por (clínica, " +
            "e-mail) explicitamente no LINQ.");
    }

    [Fact]
    public async Task StAtivaFalse_NuncaAparece_MesmoComContextoDaPropriaClinica()
    {
        // Arrange — soft delete no padrão do projeto: usuário desativado é usuário que
        // não loga mais. Se a metade StAtiva do predicado sumir, um acesso revogado
        // volta a existir.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using (var seedCtx = CreateContext(idClinicaFiltro: null, dbName))
        {
            seedCtx.UsuariosClinica.Add(new UsuarioClinica
            {
                Id = 5,
                IdClinica = 1,
                IdVeterinario = null,
                DsEmail = "demitido@clinica-a.com",
                DsSenhaHash = "$2a$11$hashDeAcessoRevogado",
                TpPerfil = PerfisUsuarioClinica.Gestor,
                StAtiva = false
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act
        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var usuarios = await ctx.UsuariosClinica.AsNoTracking().ToListAsync();

        // Assert
        usuarios.Should().HaveCount(2);
        usuarios.Should().NotContain(u => u.DsEmail == "demitido@clinica-a.com");
    }

    [Fact]
    public void Mapeamento_BateComOSchemaDaV17_NomeDeTabelaEColunas()
    {
        // Arrange — a entidade só é útil se as colunas casarem com o CREATE TABLE da
        // V17 nome a nome. Um typo aqui não falha nenhum teste de comportamento sob
        // InMemory (que ignora nomes de coluna); falha em runtime contra o Oracle, com
        // ORA-00904, muito depois. Este teste fecha essa distância.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var entityType = ctx.Model.FindEntityType(typeof(UsuarioClinica));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("USUARIO_CLINICA");

        var colunas = entityType.GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        colunas.Should().BeEquivalentTo(new[]
        {
            "ID_USUARIO_CLINICA",
            "ID_CLINICA",
            "ID_VETERINARIO",
            "DS_EMAIL",
            "DS_SENHA_HASH",
            "TP_PERFIL",
            "ST_ATIVA",
            "DT_CRIACAO",
            "DT_ATUALIZACAO"
        }, "as colunas devem bater EXATAMENTE com V17__usuario_clinica.sql — nem a mais, nem a menos");

        entityType.FindProperty(nameof(UsuarioClinica.IdVeterinario))!.IsNullable
            .Should().BeTrue("ID_VETERINARIO é nullable na V17 (gestor que não é veterinário)");
        entityType.FindProperty(nameof(UsuarioClinica.IdClinica))!.IsNullable
            .Should().BeFalse("ID_CLINICA é NOT NULL na V17 — não existe usuário sem tenant");
        entityType.FindProperty(nameof(UsuarioClinica.DsEmail))!.GetMaxLength()
            .Should().Be(120, "paridade com CLINICA.DS_EMAIL_ACESSO VARCHAR2(120)");
        entityType.FindProperty(nameof(UsuarioClinica.DsSenhaHash))!.GetMaxLength()
            .Should().Be(256, "coluna menor que a origem truncaria o hash em silêncio na conversão da V17");
        entityType.FindProperty(nameof(UsuarioClinica.TpPerfil))!.GetMaxLength()
            .Should().Be(20, "VARCHAR2(20) dimensiona papéis futuros sem ALTER de tipo");

        // F3 (fix wave pós-G2) — lacuna medida pela revisão: trocar o nome da sequence
        // por SEQ_ERRADA_QUE_NAO_EXISTE deixava a suíte 318/318 verde. O provider
        // InMemory gera as PKs sozinho e NUNCA lê esta expressão; quem quebra é o
        // Oracle, com ORA-02289, em runtime. Enquanto não houver gate contra Oracle
        // real (FD-12), esta asserção é a única coisa entre um typo aqui e a produção.
        entityType.FindProperty(nameof(UsuarioClinica.Id))!.GetDefaultValueSql()
            .Should().Be("SEQ_USUARIO_CLINICA.NEXTVAL",
                "a V17 declara DEFAULT SEQ_USUARIO_CLINICA.NEXTVAL na PK, e o padrão " +
                ".NET-owned deste projeto é sequence (V12/V12-pk-strategy-map.md) — " +
                "nome divergente vira ORA-02289 no primeiro INSERT contra Oracle");
    }

    [Fact]
    public void QueryFilter_DoModeloCompleto_TemTenantESoftDelete_EmUmFiltroSo()
    {
        // Arrange
        // Este teste olha o modelo COMPLETO (configuração + ApplyTenantFilters). Ele
        // prova o que o filtro efetivo faz — e NÃO é capaz de denunciar um filtro
        // anônimo duplicado na configuração, porque esse já teria sido substituído
        // antes de chegar aqui. Essa outra guarda é
        // Configuracao_NaoDeclaraQueryFilterProprio, abaixo. A versão anterior desta
        // classe prometia as duas coisas num teste só e entregava uma — foi o achado F1
        // da revisão G2, e é a classe de defeito mais repetida deste projeto ("check que
        // nunca executou é intenção, não cobertura"), desta vez dentro de um teste
        // escrito para preveni-la.
        using var ctx = CreateContext(idClinicaFiltro: null, Guid.NewGuid().ToString());

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(UsuarioClinica))!
            .GetDeclaredQueryFilters();

        // Assert
        // Count == 1 morde o caso "dois filtros NOMEADOS", que (medido em EF Core
        // 10.0.7) coexistem e combinam com AND em vez de se substituírem.
        filtros.Should().HaveCount(1,
            "o isolamento desta entidade tem que ser legível num predicado só; mais de " +
            "um filtro declarado significa que alguém passou a usar filtros nomeados e " +
            "a regra de composição mudou — decisão consciente, não efeito colateral");

        var texto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));
        texto.Should().Contain("IdClinicaFiltro",
            "o filtro precisa isolar por tenant, não só por StAtiva");
        texto.Should().Contain("StAtiva",
            "e precisa manter o soft delete — as duas metades vivem no mesmo predicado");
    }

    [Fact]
    public void Configuracao_NaoDeclaraQueryFilterProprio()
    {
        // Arrange
        // F1 (fix wave pós-G2). A guarda que o teste anterior PROMETIA e não cumpria.
        //
        // Medido na revisão G2 (EF Core 10.0.7): dois filtros ANÔNIMOS para a mesma
        // entidade não se combinam nem lançam — o segundo SUBSTITUI o primeiro em
        // silêncio. Como ApplyConfigurationsFromAssembly roda ANTES de
        // ApplyTenantFilters, um filtro anônimo declarado em UsuarioClinicaConfiguration
        // é apagado pelo do contexto e fica INVISÍVEL em qualquer inspeção do modelo
        // completo. (Consequência boa: o isolamento não sumiria. Consequência ruim: o
        // repo passaria a carregar código morto que parece proteger algo.)
        //
        // A única forma de enxergá-lo é montar um modelo com a configuração SOZINHA,
        // sem o ApplyTenantFilters — que é o que ContextoSoComAConfiguracao faz.
        using var ctx = new ContextoSoComAConfiguracao();

        // Act
        var filtros = ctx.Model.FindEntityType(typeof(UsuarioClinica))!
            .GetDeclaredQueryFilters();

        // Assert
        var texto = string.Join(" | ", filtros.Select(f => f.Expression?.ToString() ?? string.Empty));
        filtros.Should().BeEmpty(
            "UsuarioClinicaConfiguration não pode declarar HasQueryFilter: o filtro desta " +
            "entidade vive só em KuraDbContext.ApplyTenantFilters. Um filtro anônimo aqui " +
            "seria silenciosamente substituído por aquele (a configuração roda antes) e " +
            "viraria código morto; um filtro NOMEADO aqui derruba o build do modelo com " +
            "\"Both anonymous and named query filters cannot be applied simultaneously\". " +
            $"Filtro(s) encontrado(s): {texto}");
    }

    /// <summary>
    /// Contexto mínimo que aplica <b>apenas</b> <see cref="UsuarioClinicaConfiguration"/>
    /// — sem <c>ApplyConfigurationsFromAssembly</c> e, sobretudo, sem
    /// <c>ApplyTenantFilters</c>. Existe para que
    /// <see cref="Configuracao_NaoDeclaraQueryFilterProprio"/> consiga observar um filtro
    /// que, no modelo real, já teria sido substituído antes de qualquer asserção.
    /// </summary>
    private sealed class ContextoSoComAConfiguracao : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase($"config-only-{Guid.NewGuid()}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new UsuarioClinicaConfiguration());
    }
}
