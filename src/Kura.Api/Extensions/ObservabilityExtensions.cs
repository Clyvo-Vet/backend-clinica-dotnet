namespace Kura.Api.Extensions;

using Kura.Domain.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// S3D-04/S3D-04b: tracing entre camadas + métricas de desempenho via OpenTelemetry, com
/// exporter Console — decisão travada no backlog (nunca Application Insights, custo
/// e viola free-first do <c>CLAUDE.md</c>).
///
/// Cobertura real, medida via NuGet antes de escrever este arquivo (ver
/// task-S3D-04-report.md/task-S3D-04b-report.md para os comandos exatos):
/// - <c>OpenTelemetry.Extensions.Hosting</c> 1.18.0 — estável.
/// - <c>OpenTelemetry.Instrumentation.AspNetCore</c> 1.18.0 — estável. Produz o span de
///   BORDA por requisição HTTP (entrada) e as métricas de duração/contagem por status
///   que a rubrica pede literalmente.
/// - 🆕 S3D-04b — <c>KuraActivitySource</c> (<c>Kura.Domain.Observability</c>,
///   <c>System.Diagnostics.ActivitySource</c>, BCL pura, zero dependência nova): cobre
///   o que o G2 da S3D-04 mediu como faltante — hierarquia PAI/FILHO real entre as
///   camadas do próprio projeto (Application → Infrastructure), não só a borda HTTP.
///   Registrado abaixo via <c>.AddSource(KuraActivitySource.NomeFonte)</c>. Instrumenta
///   deliberadamente só um fluxo representativo (<c>AgendaService.GetAgendaAsync</c> →
///   <c>AgendaReadRepository.GetByIntervaloAsync</c>), não o projeto inteiro — ver
///   task-S3D-04b-report.md para o porquê da escolha.
/// - <c>OpenTelemetry.Instrumentation.EntityFrameworkCore</c> — continua NÃO incluído.
///   Toda a série de versões publicada no NuGet (até 1.18.0-beta.1, a mesma major da
///   linha estável acima) é prerelease; não existe nenhuma tag estável desse pacote.
///   Incluir um pacote prerelease para "cobrir a camada de dados" seria fingir uma
///   garantia que o próprio mantenedor não dá. Mesmo se estivesse estável, produziria
///   um span de COMANDO SQL (camada de dados), não de fronteira arquitetural — é
///   exatamente o motivo pelo qual o <c>KuraActivitySource</c> acima é a escolha certa
///   para "entre camadas" no sentido que a rubrica pede.
/// - 🆕 S3D-04b — <c>OpenTelemetry.Instrumentation.Http</c> 1.18.0 — estável (medido via
///   NuGet flatcontainer antes de incluir, ver relatório). Cobre a BORDA DE SAÍDA: a
///   chamada HTTP do healthcheck da Luna (S3D-03) e qualquer outra chamada via
///   <c>HttpClient</c> agora vira span, em vez de invisível como o G2 da S3D-04
///   registrou (achado Minor #3).
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
                .AddSource(KuraActivitySource.NomeFonte)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
