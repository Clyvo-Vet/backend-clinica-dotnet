using Kura.Domain.Exceptions;
using System.Text.Json;

namespace Kura.Api.Middlewares;

/// <summary>
/// Captura exceções não tratadas, registra via ILogger (observabilidade)
/// e devolve resposta RFC 7807 ao cliente.
///
/// NOTA: este middleware NÃO escreve em LOG_ERRO — essa tabela é
/// exclusiva do domínio PL/SQL (rubrica FIAP de Banco). Logs operacionais
/// HTTP vivem em stdout/Serilog/observabilidade externa.
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var statusCode = ex switch
        {
            EntidadeNaoEncontradaException => StatusCodes.Status404NotFound,
            RegraDeNegocioException => StatusCodes.Status422UnprocessableEntity,
            ConflitoConcorrenciaException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        _logger.Log(
            statusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            ex,
            "Exception caught by middleware. Endpoint={Endpoint} Method={Method} Status={Status} ClinicaId={ClinicaId}",
            context.Request.Path,
            context.Request.Method,
            statusCode,
            context.User?.FindFirst("clinicaId")?.Value ?? "ANONYMOUS");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = ex.GetType().Name,
            title = ex.Message,
            status = statusCode,
            traceId = context.TraceIdentifier
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(problem);
        await context.Response.Body.WriteAsync(bytes);
    }
}
