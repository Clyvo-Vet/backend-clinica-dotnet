namespace Kura.Infrastructure.Tests;

using System.Linq;
using FluentAssertions;
using Kura.Api.Extensions;
using Kura.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// TASK-84: prova de mordida (bite-proof) de que
/// <see cref="RequestPipelineExtensions.UseRequestLoggingAndExceptionHandling"/> —
/// a mesma extensão chamada por <c>Program.cs</c> — produz uma linha de log do
/// Serilog cujo TEXTO e NÍVEL batem com o status HTTP de verdade devolvido ao
/// cliente, para exceções mapeadas (4xx) e não mapeadas (5xx genuíno).
///
/// Antes da TASK-84, <c>ExceptionHandlerMiddleware</c> era registrado ANTES de
/// <c>UseSerilogRequestLogging()</c> em <c>Program.cs</c> — o Serilog logava
/// "responded 500" em nível Error para TODA exceção, mapeada ou não, porque
/// intercepta a exceção antes dela virar resposta 4xx. Ver CLAUDE.md/
/// KURA_BACKLOG_FIX_7.md para o histórico completo (quase reprovou o G4 do FIX_6).
///
/// Esta suíte não usa <c>WebApplicationFactory&lt;Program&gt;</c> porque
/// <c>Program.cs</c> tenta conectar no Oracle logo depois de <c>builder.Build()</c>
/// (checagem de migrations pendentes, com retry de até 10 tentativas / backoff até
/// 60s) — inviável sem Docker/Oracle rodando (estavam fora do ar no momento desta
/// task). Em vez disso, constrói um <see cref="WebApplication"/> mínimo que chama a
/// MESMA extensão de produção usada por <c>Program.cs</c>, isolando exatamente o
/// trecho sob teste (ordem de middleware + Serilog) do bootstrap de infraestrutura
/// não relacionado. Isso NÃO prova o pipeline completo de <c>Program.cs</c> —
/// prova o comportamento real da extensão que <c>Program.cs</c> invoca.
/// </summary>
public class RequestLoggingPipelineTests : IAsyncLifetime
{
    /// <summary>Captura os LogEvent brutos do Serilog, sem passar por sink de texto.</summary>
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly CapturingSink _sink = new();
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink)
            .CreateLogger();

        // Serilog.AspNetCore's RequestLoggingMiddleware resolves its logger from the
        // AMBIENT static Serilog.Log.Logger, not from DI's ILogger<T> — confirmed
        // empirically while writing this test (the middleware emitted nothing at all
        // until this line was added, with zero Serilog.Debugging.SelfLog errors).
        // Program.cs's real UseSerilog((ctx, sp, cfg) => ...) delegate overload sets
        // Log.Logger as a side effect by default (preserveStaticLogger defaults to
        // false), so production gets this wiring "for free" — here it must be explicit.
        Log.Logger = serilogLogger;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog(serilogLogger, dispose: true);

        var app = builder.Build();

        // Mesma extensão chamada por Program.cs — não uma reimplementação paralela.
        app.UseRequestLoggingAndExceptionHandling();

        app.MapGet("/regra-de-negocio", () => { throw new RegraDeNegocioException("violação de regra de negócio"); });
        app.MapGet("/nao-encontrado", () => { throw new EntidadeNaoEncontradaException("Pet", 42); });
        app.MapGet("/nao-mapeada", () => { throw new InvalidOperationException("bug genuíno não mapeado"); });
        app.MapGet("/ok", () => Results.Ok("tudo certo"));

        await app.StartAsync();

        _host = app;
        _client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>Acha a linha de log emitida pelo Serilog.AspNetCore RequestLoggingMiddleware
    /// (mensagem "HTTP {Method} {Path} responded {StatusCode}..."), distinta da linha
    /// separada que o próprio ExceptionHandlerMiddleware escreve ("Exception caught by
    /// middleware...").</summary>
    private LogEvent EncontrarLinhaDeRequestLogging()
    {
        var candidatas = _sink.Events
            .Where(e => e.MessageTemplate.Text.Contains("responded", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidatas.Count != 1)
        {
            Console.WriteLine($"DEBUG total events={_sink.Events.Count}");
            foreach (var e in _sink.Events)
                Console.WriteLine($"DEBUG event: level={e.Level} template=\"{e.MessageTemplate.Text}\"");
        }

        candidatas.Should().ContainSingle(
            "cada requisição deve produzir exatamente uma linha de request-logging do Serilog");
        return candidatas[0];
    }

    [Fact]
    public async Task RegraDeNegocio_Devolve422_LogNaoMenteEDizNivelWarning()
    {
        var response = await _client.GetAsync("/regra-de-negocio");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 422",
            "o texto do log precisa refletir o status real devolvido ao cliente");
        mensagem.Should().NotContain("responded 500",
            "TASK-84: esta é a armadilha de diagnóstico original — 422 real logado como 500");
        linha.Level.Should().Be(LogEventLevel.Warning,
            "4xx é aviso, não erro de servidor — nível Error aqui é falso positivo (quase reprovou o G4 do FIX_6)");
    }

    [Fact]
    public async Task EntidadeNaoEncontrada_Devolve404_LogNaoMenteEDizNivelWarning()
    {
        var response = await _client.GetAsync("/nao-encontrado");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 404");
        mensagem.Should().NotContain("responded 500");
        linha.Level.Should().Be(LogEventLevel.Warning);
    }

    [Fact]
    public async Task ExcecaoNaoMapeada_Devolve500_LogContinuaDizendo500ENivelError()
    {
        // Prova a metade que NÃO pode regredir: um 500 genuíno (bug real, exceção não
        // mapeada por ExceptionHandlerMiddleware) precisa continuar soando como erro de
        // verdade. Trocar o falso positivo (4xx como 500/Error) por um falso negativo
        // (5xx real escondido) seria pior do que o bug original.
        var response = await _client.GetAsync("/nao-mapeada");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 500");
        linha.Level.Should().Be(LogEventLevel.Error,
            "5xx real é exatamente o que o G4 precisa continuar pegando como [ERR]");
    }

    [Fact]
    public async Task RequisicaoComSucesso_Devolve200_LogNivelInformation()
    {
        var response = await _client.GetAsync("/ok");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var linha = EncontrarLinhaDeRequestLogging();
        linha.RenderMessage().Should().Contain("responded 200");
        linha.Level.Should().Be(LogEventLevel.Information);
    }
}
