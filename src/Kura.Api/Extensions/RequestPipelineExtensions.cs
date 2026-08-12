namespace Kura.Api.Extensions;

using Kura.Api.Middlewares;
using Serilog;
using Serilog.Events;

/// <summary>
/// TASK-84: registra o logging de requisição (Serilog) e o
/// <see cref="ExceptionHandlerMiddleware"/> na ordem que faz o texto e o nível do
/// log baterem com a resposta HTTP de verdade.
///
/// Antes desta task, <c>Program.cs</c> registrava
/// <c>ExceptionHandlerMiddleware</c> ANTES de <c>UseSerilogRequestLogging()</c> — o
/// que, na pilha "onion" do ASP.NET Core, torna o middleware de exceção o mais
/// EXTERNO. Numa <c>RegraDeNegocioException</c>/<c>EntidadeNaoEncontradaException</c>
/// (que o middleware converte em 422/404/409/401, nunca relançando), o Serilog via
/// <c>RequestLoggingMiddleware</c> é o mais INTERNO: ele intercepta a exceção antes
/// dela chegar ao <c>ExceptionHandlerMiddleware</c>, e o pacote
/// <c>Serilog.AspNetCore</c> loga, no caminho de exceção, um <c>500</c> literal
/// hardcoded (não lê <c>Response.StatusCode</c> nesse caminho) com nível
/// <c>Error</c> incondicional — produzindo "[ERR] ... responded 500" para uma
/// resposta que na verdade foi 4xx. Isso já quase reprovou o G4 do FIX_6 (ver
/// CLAUDE.md/KURA_BACKLOG_FIX_7.md).
///
/// A correção é de ORDEM, não de nível: com <c>UseSerilogRequestLogging()</c> por
/// FORA (registrado primeiro) e <c>ExceptionHandlerMiddleware</c> por dentro, o
/// middleware de exceção converte a exceção em resposta e NUNCA relança — então,
/// do ponto de vista do Serilog, o <c>next()</c> completa com sucesso e
/// <c>Response.StatusCode</c> já contém o valor final (422/404/409/401, ou 500 se a
/// exceção não for mapeada). O <c>GetLevel</c> customizado usa só esse status —
/// nunca mais precisa inspecionar tipo de exceção, porque o
/// <c>ExceptionHandlerMiddleware</c> é a fonte da verdade consolidada.
///
/// Invariante que este arranjo depende, e que não pode regredir:
/// <see cref="ExceptionHandlerMiddleware"/> SEMPRE escreve uma resposta e NUNCA
/// relança (ver <c>HandleExceptionAsync</c>). Se isso mudar, a garantia acima quebra.
/// </summary>
public static class RequestPipelineExtensions
{
    public static WebApplication UseRequestLoggingAndExceptionHandling(this WebApplication app)
    {
        app.UseSerilogRequestLogging(opts =>
        {
            opts.GetLevel = (ctx, _, ex) => ex != null || ctx.Response.StatusCode > 499
                ? LogEventLevel.Error
                : ctx.Response.StatusCode > 399
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
        });

        app.UseMiddleware<ExceptionHandlerMiddleware>();

        return app;
    }
}
