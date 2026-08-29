namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-03 (ciclo FIN) — prova de mordida da consulta que o login usa para resolver o usuário.
///
/// <para><b>O que está sob teste, e por que ele existe separado do teste de isolamento da
/// FD-02:</b> <c>UsuarioClinicaTenantIsolationTests</c> prova que o query filter de tenant
/// FUNCIONA. Este prova que <c>BuscarAtivosPorEmailAsync</c> <b>não depende dele</b> —
/// deliberadamente. São afirmações diferentes, e a segunda não decorre da primeira.</para>
///
/// <para>🔴 <b>O caminho que morde.</b> <c>POST /api/v1/auth/login</c> é
/// <c>[AllowAnonymous]</c>, mas <c>UseAuthentication()</c> valida e popula
/// <c>HttpContext.User</c> mesmo assim quando o cliente manda um <c>Authorization</c> ainda
/// válido — trocar de conta sem limpar o header é o caso trivial. Nesse instante
/// <c>IdClinicaFiltro</c> é NÃO nulo <b>durante o login</b>, e o filtro de tenant, que existe
/// para proteger leituras autenticadas, passaria a escopar a busca por e-mail na clínica
/// ERRADA: o usuário certo, com a senha certa, receberia "Email ou senha inválidos.".</para>
/// </summary>
public class UsuarioClinicaRepositoryLoginTests
{
    private static KuraDbContext CriarContexto(long? idClinicaFiltro, string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static UsuarioClinica NovoUsuario(long id, long idClinica, string email, bool ativa = true) => new()
    {
        Id = id,
        IdClinica = idClinica,
        IdVeterinario = null,
        DsEmail = email,
        DsSenhaHash = "$2a$11$hashQualquer",
        TpPerfil = PerfisUsuarioClinica.Gestor,
        StAtiva = ativa
    };

    private static async Task SemearAsync(string dbName, params UsuarioClinica[] usuarios)
    {
        using var ctx = CriarContexto(idClinicaFiltro: null, dbName);
        ctx.UsuariosClinica.AddRange(usuarios);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida.</b> O usuário da clínica 1 é encontrado mesmo com o contexto
    /// apontando para a clínica 2.
    ///
    /// <para><b>Controle positivo:</b> o teste irmão logo abaixo mede que, no MESMO banco e
    /// com o MESMO contexto, uma consulta que passa pelo query filter devolve <b>zero</b>
    /// linhas. Sem esse par, "achou 1" não distinguiria "o <c>IgnoreQueryFilters</c>
    /// funciona" de "o filtro estava desligado de qualquer jeito".</para>
    /// </summary>
    [Fact]
    public async Task BuscarAtivosPorEmailAsync_ComContextoDeOutraClinica_AindaAchaOUsuario()
    {
        // Arrange
        var db = $"usuario-login-{Guid.NewGuid():N}";
        await SemearAsync(db, NovoUsuario(1, idClinica: 1, "pessoa@kura.test"));

        using var ctx = CriarContexto(idClinicaFiltro: 2, db);
        var repo = new UsuarioClinicaRepository(ctx);

        // Act
        var achados = await repo.BuscarAtivosPorEmailAsync("pessoa@kura.test");

        // Assert
        achados.Should().ContainSingle(
            "o login não pode depender de estado ambiente que ele não controla — " +
            "um Authorization residual da sessão anterior escoparia a busca na clínica errada");
        achados[0].IdClinica.Should().Be(1);
    }

    /// <summary>
    /// Metade "1" do controle positivo do teste acima: MESMO banco, MESMO contexto, consulta
    /// SUJEITA ao query filter. Se esta consulta achasse o usuário, o teste acima não estaria
    /// provando nada sobre <c>IgnoreQueryFilters</c>.
    /// </summary>
    [Fact]
    public async Task ControlePositivo_MesmoContexto_ConsultaSujeitaAoFiltroNaoAchaNada()
    {
        // Arrange
        var db = $"usuario-login-{Guid.NewGuid():N}";
        await SemearAsync(db, NovoUsuario(1, idClinica: 1, "pessoa@kura.test"));

        using var ctx = CriarContexto(idClinicaFiltro: 2, db);

        // Act — sem IgnoreQueryFilters, o filtro de tenant está de fato ATIVO neste contexto.
        var achados = await ctx.UsuariosClinica
            .Where(u => u.DsEmail == "pessoa@kura.test")
            .ToListAsync();

        // Assert
        achados.Should().BeEmpty(
            "o filtro de tenant ESTÁ ativo aqui — é isso que torna o teste irmão uma prova");
    }

    /// <summary>
    /// O <c>StAtiva</c> que vinha embutido no query filter foi reescrito à mão no predicado do
    /// repositório. Este teste é o que impede que ele seja apagado junto com o filtro:
    /// usuário desativado (soft delete) não autentica.
    ///
    /// <para><b>Controle positivo:</b> o mesmo banco tem um usuário ATIVO com outro e-mail,
    /// encontrado normalmente — então o vazio não é "a consulta não acha nada".</para>
    /// </summary>
    [Fact]
    public async Task BuscarAtivosPorEmailAsync_UsuarioDesativado_NaoEhRetornado()
    {
        // Arrange
        var db = $"usuario-login-{Guid.NewGuid():N}";
        await SemearAsync(db,
            NovoUsuario(1, idClinica: 1, "demitido@kura.test", ativa: false),
            NovoUsuario(2, idClinica: 1, "ativo@kura.test"));

        using var ctx = CriarContexto(idClinicaFiltro: null, db);
        var repo = new UsuarioClinicaRepository(ctx);

        // Act
        var desativado = await repo.BuscarAtivosPorEmailAsync("demitido@kura.test");
        var ativo = await repo.BuscarAtivosPorEmailAsync("ativo@kura.test");

        // Assert
        desativado.Should().BeEmpty("soft delete é regra do projeto — usuário inativo não loga");
        ativo.Should().ContainSingle("controle positivo: a consulta acha quem está ativo");
    }

    /// <summary>
    /// O mesmo e-mail em duas clínicas é estado LEGAL (a UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c>). O repositório devolve <b>as duas</b> linhas — quem
    /// decide o que fazer com N&gt;1 é o <c>AuthService</c>, explicitamente. Um repositório
    /// que já filtrasse aqui esconderia a decisão.
    /// </summary>
    [Fact]
    public async Task BuscarAtivosPorEmailAsync_MesmoEmailEmDuasClinicas_DevolveAsDuas()
    {
        // Arrange
        var db = $"usuario-login-{Guid.NewGuid():N}";
        await SemearAsync(db,
            NovoUsuario(1, idClinica: 2, "atende-nas-duas@kura.test"),
            NovoUsuario(2, idClinica: 1, "atende-nas-duas@kura.test"));

        using var ctx = CriarContexto(idClinicaFiltro: null, db);
        var repo = new UsuarioClinicaRepository(ctx);

        // Act
        var achados = await repo.BuscarAtivosPorEmailAsync("atende-nas-duas@kura.test");

        // Assert
        achados.Should().HaveCount(2);
        // Ordenação determinística; ela NÃO escolhe nada — a decisão sobre N>1 é do
        // AuthService, que falha explicitamente.
        achados.Select(u => u.IdClinica).Should().ContainInOrder(1L, 2L);
    }
}
