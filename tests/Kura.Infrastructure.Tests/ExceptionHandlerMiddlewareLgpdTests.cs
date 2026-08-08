namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// TASK-67, fix round 1 (Important-1 da revisão cética): prova de verdade — não
/// afirmação em prosa — de que o telefone do tutor não vaza para o log de aplicação
/// via <see cref="ExceptionHandlerMiddleware"/>.
///
/// A revisão encontrou que a versão anterior de <c>LgpdNaoVazamentoTests.cs</c>
/// justificava o item de aceite com a frase "o middleware nunca lê campos do request
/// diretamente" — **falsa**: o middleware loga <c>context.Request.Path</c>
/// integralmente (linha antiga 54), e GET /api/v1/tutores/telefone/{numero} carrega o
/// telefone no path, não no body. Este teste substitui a alegação por exercício real
/// do middleware, com um logger capturador (não um mock que verifica chamada — lê o
/// texto formatado de verdade, igual ao que um provider real registraria).
/// </summary>
public class ExceptionHandlerMiddlewareLgpdTests
{
    private const string TelefoneSensivel = "5511999990000";

    /// <summary>
    /// <see cref="ILogger{T}"/> fake que captura o texto já formatado (mensagem com os
    /// argumentos substituídos), igual ao que sai pro console/Serilog de verdade —
    /// diferente de um mock que só verificaria "Log foi chamado com estes objetos",
    /// que não provaria nada sobre o texto final.
    /// </summary>
    private sealed class CapturingLogger : ILogger<ExceptionHandlerMiddleware>
    {
        public List<string> MensagensFormatadas { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            MensagensFormatadas.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static (ExceptionHandlerMiddleware middleware, CapturingLogger logger) CreateSut(Exception paraLancar)
    {
        var logger = new CapturingLogger();
        Task Next(HttpContext _) => throw paraLancar;
        var middleware = new ExceptionHandlerMiddleware(Next, logger);
        return (middleware, logger);
    }

    [Fact]
    public async Task GetTelefone_ExcecaoNoEndpoint_TelefoneNaoApareceNoLogFormatado()
    {
        var (middleware, logger) = CreateSut(new InvalidOperationException("falha simulada de infraestrutura"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = $"/api/v1/tutores/telefone/{TelefoneSensivel}";
        httpContext.Request.Method = "GET";
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        logger.MensagensFormatadas.Should().ContainSingle();
        logger.MensagensFormatadas[0].Should().NotContain(TelefoneSensivel,
            "GET /tutores/telefone/{numero} carrega o telefone no PATH — o middleware " +
            "precisa redigir o segmento antes de logar, não só evitar interpolar o body");
        logger.MensagensFormatadas[0].Should().Contain("/api/v1/tutores/telefone/{redacted}");
    }

    [Fact]
    public void RedigirPathSensivel_RotaTelefone_RedigeApenasONumero()
    {
        var resultado = ExceptionHandlerMiddleware.RedigirPathSensivel(
            new PathString($"/api/v1/tutores/telefone/{TelefoneSensivel}"));

        resultado.Should().Be("/api/v1/tutores/telefone/{redacted}");
        resultado.Should().NotContain(TelefoneSensivel);
    }

    [Fact]
    public void RedigirPathSensivel_OutraRota_PassaIntocada()
    {
        // Prova que a redação é escopada — não é um "apague tudo que parece PII"
        // genérico que degradaria a utilidade do log pras outras 20+ rotas do repo.
        var resultado = ExceptionHandlerMiddleware.RedigirPathSensivel(
            new PathString("/api/v1/tutores/42/pets"));

        resultado.Should().Be("/api/v1/tutores/42/pets");
    }

    [Fact]
    public async Task OutroEndpoint_ExcecaoQualquer_PathContinuaCompletoNoLog()
    {
        // Regressão: a correção do Important-1 não pode apagar path útil de todo o
        // resto da API — só do segmento comprovadamente sensível.
        var (middleware, logger) = CreateSut(new InvalidOperationException("erro qualquer"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/pets/42";
        httpContext.Request.Method = "GET";
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        logger.MensagensFormatadas[0].Should().Contain("/api/v1/pets/42");
    }

    [Fact]
    public async Task GetTelefone_ExcecaoNoEndpoint_CorpoDaRespostaTambemNaoContemTelefone()
    {
        // O corpo (RFC 7807) só expõe ex.Message — mas prova de verdade em vez de
        // assumir, já que é o outro lugar por onde PII poderia escapar.
        var (middleware, _) = CreateSut(new InvalidOperationException("falha simulada"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = $"/api/v1/tutores/telefone/{TelefoneSensivel}";
        httpContext.Request.Method = "GET";
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await middleware.InvokeAsync(httpContext);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var corpo = await reader.ReadToEndAsync();

        corpo.Should().NotContain(TelefoneSensivel);
    }
}
