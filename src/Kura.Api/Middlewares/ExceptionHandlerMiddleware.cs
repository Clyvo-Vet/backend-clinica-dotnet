namespace Kura.Api.Middlewares;

using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
            var statusCode = ex switch
            {
                EntidadeNaoEncontradaException => StatusCodes.Status404NotFound,
                RegraDeNegocioException => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };

            _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);

            try
            {
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<KuraDbContext>();

                var logErro = new LogErro
                {
                    DsEndpoint = context.Request.Path,
                    DsMetodo = context.Request.Method,
                    DsMensagem = ex.Message.Length > 500
                        ? ex.Message[..500]
                        : ex.Message,
                    DsStackTrace = ex.StackTrace,
                    NrStatusCode = statusCode,
                    DtOcorrencia = DateTime.UtcNow,
                };

                dbContext.LogsErro.Add(logErro);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Falha ao persistir LogErro no banco de dados.");
            }

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = ex.Message,
                    status = statusCode,
                    type = ex.GetType().Name
                });
            }
            else
            {
                _logger.LogWarning("Response already started, cannot write error response for {ExceptionType}", ex.GetType().Name);
            }
        }
    }
}
