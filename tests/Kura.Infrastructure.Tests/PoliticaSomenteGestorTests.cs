namespace Kura.Infrastructure.Tests;

using System.Security.Claims;
using FluentAssertions;
using Kura.Api.Extensions;
using Kura.Api.Services;
using Kura.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FD-04 — a política <c>SomenteGestor</c>, avaliada <b>diretamente</b> pelo
/// <see cref="IAuthorizationService"/>, sobre o registro de PRODUÇÃO
/// (<see cref="AuthorizationExtensions.AddKuraAuthorization"/>). Mesmo padrão de
/// <c>RequestLoggingPipelineTests</c>: o teste chama a extensão que o <c>Program.cs</c>
/// chama, então uma mudança na política de produção aparece aqui — um teste que montasse a
/// própria política provaria a cópia, não o produto.
///
/// <para>
/// 🔴 <b>O caso que decide a segurança desta task é
/// <see cref="Token_sem_a_claim_perfil_NAO_e_gestor"/>.</b> <c>IClinicaContext.Perfil</c> é
/// <c>string?</c> e é <c>null</c> para todo token emitido antes da FD-03 — tokens que
/// <b>continuam válidos até expirar</b>. Um principal autenticado sem a claim <c>perfil</c>
/// não pode ser tratado como gestor.
/// </para>
///
/// <para>
/// A classe carrega, ao lado da política real, a <b>formulação inversa</b> que estava
/// disponível e parece equivalente (lista de negação: "não é veterinário, logo é gestor").
/// Ela existe aqui como <b>controle</b>: os testes de GESTOR e de VETERINARIO passam
/// identicamente nas duas, e só o token pré-FD-03 as separa —
/// <see cref="A_formulacao_por_lista_de_negacao_concederia_acesso_ao_token_antigo"/> mede
/// isso em vez de afirmar. Sem esse controle, "a política funciona" seria compatível com uma
/// política que falha aberta.
/// </para>
/// </summary>
public class PoliticaSomenteGestorTests
{
    /// <summary>Nome da política de controle — NÃO registrada em produção.</summary>
    private const string ListaDeNegacaoDeControle = "ControleListaDeNegacao";

    private static IAuthorizationService CriarServicoDeAutorizacao()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Registro de PRODUÇÃO. É esta chamada que o Program.cs faz.
        services.AddKuraAuthorization();

        // Política de CONTROLE, adicionada por cima: a formulação por lista de negação, que é
        // a alternativa plausível e errada. AddAuthorization é aditivo, então isto não altera
        // a política de produção — e isso não é suposição: em
        // A_formulacao_por_lista_de_negacao_concederia_acesso_ao_token_antigo as DUAS
        // políticas são consultadas no MESMO provider, e a real continua negando.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ListaDeNegacaoDeControle, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    ctx.User.FindFirst(ClinicaContext.ClaimPerfil)?.Value
                        != PerfisUsuarioClinica.Veterinario));
        });

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    /// <summary>Principal AUTENTICADO com as claims informadas.</summary>
    private static ClaimsPrincipal Autenticado(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    /// <summary>
    /// Claims de um token FD-03 completo. O <c>clinicaId</c> entra porque é o que um token
    /// real carrega — deixá-lo de fora tornaria o cenário mais fácil do que a realidade.
    /// </summary>
    private static Claim[] ClaimsFd03(string perfil) =>
    [
        new("clinicaId", "1"),
        new(ClinicaContext.ClaimPerfil, perfil),
    ];

    /// <summary>
    /// Claims de um token emitido ANTES da FD-03: assinatura válida, não expirado, e
    /// <b>sem</b> a claim <c>perfil</c>. Espelha <c>AutenticacaoHelper.GerarTokenPreFd03</c>.
    /// </summary>
    private static Claim[] ClaimsPreFd03() =>
    [
        new("clinicaId", "1"),
        new("veterinarioId", "1"),
    ];

    [Fact]
    public async Task Perfil_GESTOR_e_autorizado()
    {
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            Autenticado(ClaimsFd03(PerfisUsuarioClinica.Gestor)),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Perfil_VETERINARIO_e_negado()
    {
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            Autenticado(ClaimsFd03(PerfisUsuarioClinica.Veterinario)),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Token_sem_a_claim_perfil_NAO_e_gestor()
    {
        // 🔴 FALHA FECHADA. Token pré-FD-03: autenticado, válido, sem `perfil`.
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            Autenticado(ClaimsPreFd03()),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeFalse(
            "a política é lista de PERMISSÃO: claim ausente é negação, não omissão");
    }

    [Fact]
    public async Task Principal_anonimo_e_negado()
    {
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Claim_de_perfil_com_valor_desconhecido_e_negada()
    {
        // Um papel futuro (RECEPCIONISTA) não vira gestor por não ser veterinário.
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            Autenticado(ClaimsFd03("RECEPCIONISTA")),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Claim_de_role_padrao_do_aspnet_NAO_substitui_a_claim_perfil()
    {
        // Prova de que a política lê `perfil`, e não ClaimTypes.Role: um principal que só
        // carregue a role padrão do ASP.NET com o valor GESTOR continua negado. É o inverso
        // do argumento que descartou [Authorize(Roles=...)] — as duas claims não são
        // intercambiáveis, e depender disso em silêncio seria a forma de a política parecer
        // funcionar por acidente.
        var sut = CriarServicoDeAutorizacao();

        var resultado = await sut.AuthorizeAsync(
            Autenticado(new Claim("clinicaId", "1"),
                        new Claim(ClaimTypes.Role, PerfisUsuarioClinica.Gestor)),
            resource: null,
            PoliticasAutorizacao.SomenteGestor);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_formulacao_por_lista_de_negacao_concederia_acesso_ao_token_antigo()
    {
        // 🔴 CONTROLE. Este é o teste que dá sentido ao verde de
        // Token_sem_a_claim_perfil_NAO_e_gestor: ele mede que a alternativa plausível FALHA
        // ABERTA no mesmo cenário. Sem ele, "a política nega token antigo" poderia ser um
        // acidente de configuração em vez de uma escolha.
        var sut = CriarServicoDeAutorizacao();
        var tokenAntigo = Autenticado(ClaimsPreFd03());

        var pelaListaDeNegacao =
            await sut.AuthorizeAsync(tokenAntigo, resource: null, ListaDeNegacaoDeControle);
        var pelaPoliticaReal =
            await sut.AuthorizeAsync(tokenAntigo, resource: null, PoliticasAutorizacao.SomenteGestor);

        pelaListaDeNegacao.Succeeded.Should().BeTrue(
            "null != \"VETERINARIO\" é true — a lista de negação promove o token antigo a gestor");
        pelaPoliticaReal.Succeeded.Should().BeFalse(
            "e é exatamente por isso que a política de produção é RequireClaim");
    }

    [Fact]
    public async Task As_duas_formulacoes_sao_indistinguiveis_nos_casos_de_GESTOR_e_VETERINARIO()
    {
        // Fecha o argumento: as duas políticas concordam em todo cenário com a claim
        // presente. Só o token antigo as separa — logo, um conjunto de testes que só
        // exercitasse GESTOR e VETERINARIO não conseguiria distinguir a política correta da
        // que falha aberta.
        var sut = CriarServicoDeAutorizacao();

        foreach (var perfil in new[] { PerfisUsuarioClinica.Gestor, PerfisUsuarioClinica.Veterinario })
        {
            var principal = Autenticado(ClaimsFd03(perfil));

            var real = await sut.AuthorizeAsync(
                principal, resource: null, PoliticasAutorizacao.SomenteGestor);
            var controle = await sut.AuthorizeAsync(
                principal, resource: null, ListaDeNegacaoDeControle);

            real.Succeeded.Should().Be(controle.Succeeded,
                $"as duas formulações concordam para o perfil {perfil}");
        }
    }

    [Fact]
    public async Task A_politica_de_producao_existe_com_o_nome_que_o_controller_referencia()
    {
        // Guarda contra o registro sumir sem ninguém perceber: sem a política, todo endpoint
        // de UsuariosClinicaController morreria em runtime com InvalidOperationException.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKuraAuthorization();

        var provider = services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationPolicyProvider>();

        var politica = await provider.GetPolicyAsync(PoliticasAutorizacao.SomenteGestor);

        politica.Should().NotBeNull();
    }
}
