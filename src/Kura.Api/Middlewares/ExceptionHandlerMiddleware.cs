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
///
/// TASK-67 fix round 1 (Important-1 da revisão): este middleware loga
/// <c>context.Request.Path</c> integralmente. GET /api/v1/tutores/telefone/{numero}
/// (TASK-67) carrega o telefone do tutor **no próprio path**, não no body — então
/// qualquer exceção nesse endpoint (timeout Oracle, NRE, o que for) gravava o telefone
/// cru no log de aplicação, violação direta da restrição de LGPD deste projeto. Fix:
/// <see cref="RedigirPathSensivel"/> redige o segmento variável antes de logar. O
/// corpo da resposta HTTP (`problem.title = ex.Message`) nunca incluiu o path, então
/// não precisou de mudança.
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    // Prefixos de rota conhecidos por carregarem PII diretamente no segmento de path
    // (não no body). Lista pequena e explícita de propósito — quem adicionar uma rota
    // nova com PII no path (ex.: .../cpf/{numero}) precisa lembrar de somar aqui.
    private static readonly string[] SegmentosSensiveis = ["/tutores/telefone/"];

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

    /// <summary>
    /// Redige o segmento variável de rotas conhecidas por carregar PII no path.
    /// "/api/v1/tutores/telefone/5511999990000" vira
    /// "/api/v1/tutores/telefone/{redacted}"; qualquer outro path passa intocado.
    /// </summary>
    public static string RedigirPathSensivel(PathString path)
    {
        var valor = path.Value ?? string.Empty;
        foreach (var marcador in SegmentosSensiveis)
        {
            var indice = valor.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
            if (indice >= 0)
                return valor[..(indice + marcador.Length)] + "{redacted}";
        }
        return valor;
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
            RedigirPathSensivel(context.Request.Path),
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
