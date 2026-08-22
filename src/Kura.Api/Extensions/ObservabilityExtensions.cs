namespace Kura.Api.Extensions;

using Kura.CrossCutting.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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
/// - 🆕 S3D-04b — <c>KuraActivitySource</c> (<c>Kura.CrossCutting.Observability</c>,
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
/// - 🆕 S3D-04c — <c>ConfigureResource(r =&gt; r.AddService(...))</c>: antes desta task todo
///   span/métrica saía com <c>service.name: unknown_service:Kura.Api</c>, o fallback que a
///   OTel SDK inventa a partir do nome do processo quando ninguém declara o recurso. O
///   recurso é declarado UMA vez, no nível do <c>AddOpenTelemetry()</c>, e vale para as duas
///   pilhas. ⚠️ A forma alternativa — <c>SetResourceBuilder(...)</c> dentro de cada provider —
///   foi tentada na 1ª volta desta task e REPROVADA no G2: a instância na pilha de métricas
///   quebra o parentesco dos spans entre camadas em runtime. Ver o comentário no corpo de
///   <c>AddKuraObservability</c> e o §5 de task-S3D-04c-report.md.
/// - Exporter Prometheus (<c>OpenTelemetry.Exporter.Prometheus.AspNetCore</c>) — NÃO
///   incluído pelo mesmo motivo: toda a série é prerelease (até 1.18.0-beta.1). O
///   Console exporter sozinho já atende ao critério de aceite (prova de emissão real
///   no log).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// S3D-04c: valor de <c>service.name</c> nos spans e métricas exportados. Sem isto a
    /// OTel SDK cai no fallback <c>unknown_service:&lt;nome do processo&gt;</c>.
    /// </summary>
    private const string NomeServico = "Kura.Api";

    /// <summary>Valor de <c>service.version</c> — mesma versão declarada no
    /// <see cref="KuraActivitySource.Instancia"/>.</summary>
    private const string VersaoServico = "1.0.0";

    public static IServiceCollection AddKuraObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            // 🔴 S3D-04c (2ª volta) — ConfigureResource NO NÍVEL DO BUILDER, um só, antes dos
            // dois providers. NÃO trocar por .SetResourceBuilder(...) dentro de WithTracing/
            // WithMetrics: a 1ª volta desta task fez exatamente isso e o G2 mediu que o
            // SetResourceBuilder da pilha de MÉTRICAS DESPARENTA os spans entre camadas — o
            // span de Infrastructure vira raiz, com TraceId próprio e sem ParentSpanId,
            // destruindo a hierarquia que a S3D-04b existe para provar. Medição pareada, 3
            // execuções por configuração, em task-S3D-04c-review.md §1 e no §5 do relatório
            // desta task. O mecanismo interno pelo qual o Resource do MeterProvider afeta o
            // parentesco de Activity do TracerProvider NÃO foi determinado por ninguém até
            // agora — o que está estabelecido é a causalidade e o ponto exato. Travado por
            // teste: ObservabilityExtensionsTests
            // .PipelineDeProducaoReal_SpansEmitidosPeloTracerProviderDoDI_...
            .ConfigureResource(r => r.AddService(
                serviceName: NomeServico,
                serviceVersion: VersaoServico))
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
