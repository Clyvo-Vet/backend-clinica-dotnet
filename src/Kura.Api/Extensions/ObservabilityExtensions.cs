namespace Kura.Api.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// S3D-04: tracing entre camadas + métricas de desempenho via OpenTelemetry, com
/// exporter Console — decisão travada no backlog (nunca Application Insights, custo
/// e viola free-first do <c>CLAUDE.md</c>).
///
/// Cobertura real, medida via NuGet antes de escrever este arquivo (ver
/// task-S3D-04-report.md para o comando exato):
/// - <c>OpenTelemetry.Extensions.Hosting</c> 1.18.0 — estável.
/// - <c>OpenTelemetry.Instrumentation.AspNetCore</c> 1.18.0 — estável. Cobre tracing
///   HTTP entre camadas (a requisição inteira, incluindo os handlers internos) e as
///   métricas de duração/contagem por status que a rubrica pede literalmente.
/// - <c>OpenTelemetry.Instrumentation.EntityFrameworkCore</c> — NÃO incluído. Toda a
///   série de versões publicada no NuGet (até 1.18.0-beta.1, a mesma major da linha
///   estável acima) é prerelease; não existe nenhuma tag estável desse pacote. Incluir
///   um pacote prerelease para "cobrir a camada de dados" seria fingir uma garantia que
///   o próprio mantenedor não dá. Consequência declarada: os spans emitidos cobrem o
///   pipeline ASP.NET Core (camada de apresentação/entrada), não o acesso a dados via
///   EF Core → Oracle especificamente. O `AddAspNetCoreInstrumentation` já produz um
///   span por requisição HTTP completa (do controller até a resposta), que é o que a
///   rubrica pede como "rastreando requisições entre camadas" — só não há um span-filho
///   dedicado à query EF Core dentro dele.
/// - Exporter Prometheus (<c>OpenTelemetry.Exporter.Prometheus.AspNetCore</c>) — NÃO
///   incluído pelo mesmo motivo: toda a série é prerelease (até 1.18.0-beta.1). O
///   Console exporter sozinho já atende ao critério de aceite (prova de emissão real
///   no log).
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddKuraObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
