namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

/// <summary>
/// TASK-67: prova (a) que <see cref="LunaApiKeyAuthFilter"/> (novo, chave
/// <c>Luna:ApiKey</c>) autentica corretamente, e (b) que <see cref="ApiKeyAuthFilter"/>
/// (pré-existente, chave <c>IoT:ApiKey</c>, consumido por
/// <see cref="Kura.Api.Controllers.IotController"/>) continua funcionando exatamente
/// como antes — nenhuma linha dele foi tocada nesta task (decisão: sibling filter em
/// vez de generalizar a classe existente, ver comentário de LunaApiKeyAuthFilter).
/// </summary>
public class ApiKeyAuthFiltersTests
{
    private static AuthorizationFilterContext CreateContext(string? headerValue, string headerName = "X-Api-Key")
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
            httpContext.Request.Headers[headerName] = headerValue;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }

    private static IConfiguration ConfigWith(string key, string? value)
    {
        var dict = value is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [key] = value };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // ── LunaApiKeyAuthFilter (novo) ──────────────────────────────────────

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveCorreta_Autoriza()
    {
        // Arrange
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("segredo-luna");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeNull("chave correta deve deixar a requisição passar (sem Result setado)");
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveErrada_Retorna401()
    {
        // Arrange
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("chave-errada");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_HeaderAusente_Retorna401()
    {
        // Arrange
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext(headerValue: null);

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveDeOutroHeader_NaoAutoriza()
    {
        // Arrange
        // Confirma que o filtro lê especificamente X-Api-Key, não qualquer header.
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext(headerValue: "segredo-luna", headerName: "Authorization");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ConfigAusente_LancaNaConstrucao()
    {
        // Act
        // Fail-fast: sem Luna:ApiKey configurado, o filtro nem deveria ser construído
        // silenciosamente (mesmo comportamento de ApiKeyAuthFilter para IoT:ApiKey).
        var act = () => new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", null));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Luna:ApiKey not configured.");
    }

    // ── TASK-86 (item 6): comparação constant-time via ApiKeyComparer ──────

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveErradaDeMesmoTamanho_Retorna401()
    {
        // Arrange
        // "chave-errada" e "segredo-luna" têm ambas 12 caracteres — exercita o
        // caminho de ApiKeyComparer.IsMatch em que os tamanhos batem e a diferença
        // só aparece no conteúdo (é o caminho que precisa ser constant-time de
        // verdade; o caminho de tamanho diferente já sai cedo por definição).
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("chave-errada");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveErradaDeTamanhoDiferente_Retorna401()
    {
        // Arrange
        // CryptographicOperations.FixedTimeEquals (usado por ApiKeyComparer) retorna
        // false imediatamente quando os tamanhos diferem — não é constant-time
        // quanto ao tamanho, só quanto ao conteúdo (limitação declarada no XML doc
        // de ApiKeyComparer). Este teste prova que o caminho de tamanho diferente
        // ainda rejeita corretamente, só não com garantia de tempo constante.
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("curta");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ConfigVazia_HeaderAusente_Retorna401()
    {
        // Arrange
        // Luna:ApiKey configurado como string vazia (não null — não dispara o
        // fail-fast do construtor) + header ausente: já rejeitado pelo
        // TryGetValue antes de chegar em ApiKeyComparer.
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", ""));
        var context = CreateContext(headerValue: null);

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ConfigVazia_HeaderVazio_AutorizaHoje_ComportamentoPreExistente()
    {
        // Arrange
        // ACHADO desta task, NÃO corrigido aqui (fora do escopo do fix de timing):
        // com Luna:ApiKey configurado como "" (string vazia) e o header enviado com
        // valor também "", o filtro AUTORIZA — "" == "" é um match válido para
        // ApiKeyComparer.IsMatch (FixedTimeEquals de dois spans vazios retorna
        // true), exatamente o mesmo resultado que o operador `!=` antigo já dava.
        // Ou seja, este teste prova que o refactor NÃO mudou o comportamento
        // pré-existente — só tornou a comparação de conteúdo constant-time.
        //
        // Isso diverge do padrão "fail closed" que a Luna usa do lado Python
        // (secrets.compare_digest com "not chave_esperada" rejeitando chave
        // configurada vazia antes mesmo de comparar, dependencies.py:44) — o .NET
        // não tem esse guard. Não corrigido aqui: mudar o comportamento de
        // "config vazia" é uma decisão de escopo maior que constant-time, e a
        // instrução desta task foi reportar divergência em vez de alterar
        // silenciosamente. Ver relatório da TASK-86 (Pacote A) para o achado.
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", ""));
        var context = CreateContext("");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeNull(
            "documenta o comportamento ATUAL (pré-existente, não introduzido por " +
            "esta task): config vazia + header vazio autoriza. Se este teste " +
            "quebrar no futuro, é uma mudança de comportamento intencional que " +
            "merece decisão explícita, não um efeito colateral.");
    }

    // ── ApiKeyAuthFilter (pré-existente, IoT — regressão) ───────────────────

    [Fact]
    public void ApiKeyAuthFilter_IoT_ChaveCorreta_ContinuaAutorizando()
    {
        // Arrange
        var filter = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var context = CreateContext("segredo-iot");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeNull(
            "IotController depende deste comportamento continuar idêntico — TASK-67 não " +
            "tocou ApiKeyAuthFilter, criou um sibling (LunaApiKeyAuthFilter) para não " +
            "correr risco de regressão aqui");
    }

    [Fact]
    public void ApiKeyAuthFilter_IoT_ChaveErrada_ContinuaRetornando401()
    {
        // Arrange
        var filter = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var context = CreateContext("chave-errada");

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void ApiKeyAuthFilter_NaoAceitaChaveDoLuna_SaoConfigsIndependentes()
    {
        // Arrange
        // Prova que os dois filtros leem chaves de config diferentes — uma chave válida
        // para Luna não autentica no IoT e vice-versa (nenhum acoplamento indevido).
        var filterIot = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var contextComChaveLuna = CreateContext("segredo-luna");

        // Act
        filterIot.OnAuthorization(contextComChaveLuna);

        // Assert
        contextComChaveLuna.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
