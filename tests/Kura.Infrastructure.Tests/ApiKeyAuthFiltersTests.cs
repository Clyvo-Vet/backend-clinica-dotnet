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
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("segredo-luna");

        filter.OnAuthorization(context);

        context.Result.Should().BeNull("chave correta deve deixar a requisição passar (sem Result setado)");
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveErrada_Retorna401()
    {
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext("chave-errada");

        filter.OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_HeaderAusente_Retorna401()
    {
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext(headerValue: null);

        filter.OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ChaveDeOutroHeader_NaoAutoriza()
    {
        // Confirma que o filtro lê especificamente X-Api-Key, não qualquer header.
        var filter = new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", "segredo-luna"));
        var context = CreateContext(headerValue: "segredo-luna", headerName: "Authorization");

        filter.OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void LunaApiKeyAuthFilter_ConfigAusente_LancaNaConstrucao()
    {
        // Fail-fast: sem Luna:ApiKey configurado, o filtro nem deveria ser construído
        // silenciosamente (mesmo comportamento de ApiKeyAuthFilter para IoT:ApiKey).
        var act = () => new LunaApiKeyAuthFilter(ConfigWith("Luna:ApiKey", null));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Luna:ApiKey not configured.");
    }

    // ── ApiKeyAuthFilter (pré-existente, IoT — regressão) ───────────────────

    [Fact]
    public void ApiKeyAuthFilter_IoT_ChaveCorreta_ContinuaAutorizando()
    {
        var filter = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var context = CreateContext("segredo-iot");

        filter.OnAuthorization(context);

        context.Result.Should().BeNull(
            "IotController depende deste comportamento continuar idêntico — TASK-67 não " +
            "tocou ApiKeyAuthFilter, criou um sibling (LunaApiKeyAuthFilter) para não " +
            "correr risco de regressão aqui");
    }

    [Fact]
    public void ApiKeyAuthFilter_IoT_ChaveErrada_ContinuaRetornando401()
    {
        var filter = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var context = CreateContext("chave-errada");

        filter.OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void ApiKeyAuthFilter_NaoAceitaChaveDoLuna_SaoConfigsIndependentes()
    {
        // Prova que os dois filtros leem chaves de config diferentes — uma chave válida
        // para Luna não autentica no IoT e vice-versa (nenhum acoplamento indevido).
        var filterIot = new ApiKeyAuthFilter(ConfigWith("IoT:ApiKey", "segredo-iot"));
        var contextComChaveLuna = CreateContext("segredo-luna");

        filterIot.OnAuthorization(contextComChaveLuna);

        contextComChaveLuna.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
