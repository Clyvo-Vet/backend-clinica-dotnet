namespace Kura.Infrastructure.Tests;

using System.Diagnostics;
using FluentAssertions;
using Kura.Api.Extensions;
using Kura.CrossCutting.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// S3D-04b: fecha o achado Important #2 do G2 da S3D-04 ("ausência total de teste
/// automatizado para AddKuraObservability()"). Prova duas coisas baratas, sem precisar
/// de Oracle/Docker: (1) o DI expõe TracerProvider/MeterProvider de verdade — a
/// regressão mais provável (alguém remove a chamada em Program.cs, ou quebra a
/// extensão) fica pega aqui, não só quando um humano notar que o log parou de emitir
/// spans; (2) o <see cref="KuraActivitySource"/> customizado, quando registrado através
/// da mesma <c>AddKuraObservability()</c> de produção, produz <c>Activity.ParentId</c>
/// REAL quando iniciado dentro de um span pai — a prova ponta a ponta, com output real
/// do Console exporter contra Oracle/API de verdade, está no relatório da task
/// (task-S3D-04b-report.md), não aqui.
///
/// Não usa <c>WebApplicationFactory&lt;Program&gt;</c> pelo mesmo motivo documentado em
/// <see cref="RequestLoggingPipelineTests"/> (TASK-84): <c>Program.cs</c> tenta conectar
/// no Oracle logo após <c>builder.Build()</c> — inviável sem Docker/Oracle no ar.
/// </summary>
public class ObservabilityExtensionsTests
{
    [Fact]
    public void AddKuraObservability_ServiceCollection_RegistraTracerProviderEMeterProviderNoDI()
    {
        var services = new ServiceCollection();

        services.AddKuraObservability();
        using var provider = services.BuildServiceProvider();

        provider.GetService<TracerProvider>().Should().NotBeNull(
            "sem TracerProvider no DI, nenhum span sai — silenciosamente, sem erro de compilação nem de runtime");
        provider.GetService<MeterProvider>().Should().NotBeNull(
            "sem MeterProvider no DI, nenhuma métrica sai — mesma classe de regressão silenciosa");
    }

    [Fact]
    public void KuraActivitySource_RegistradoViaAddKuraObservability_SpanFilhoCarregaParentIdDoSpanPaiHttp()
    {
        var services = new ServiceCollection();
        services.AddKuraObservability();
        using var provider = services.BuildServiceProvider();

        // Força o build do pipeline OTel (registra o ActivityListener global que ouve
        // "Microsoft.AspNetCore" e KuraActivitySource.NomeFonte, via .AddSource(...) em
        // ObservabilityExtensions) — resolver do DI é o que dispara isso, igual em produção.
        provider.GetRequiredService<TracerProvider>();

        // Simula o span de borda que AddAspNetCoreInstrumentation cria de verdade numa
        // requisição HTTP real — mesmo nome de fonte ("Microsoft.AspNetCore", confirmado
        // no output do Console exporter capturado pelo G2 da S3D-04), para que o
        // ActivityListener registrado acima capture este span como pai.
        using var fonteHttpSimulada = new ActivitySource("Microsoft.AspNetCore");
        using var spanDeBorda = fonteHttpSimulada.StartActivity("GET /api/v1/agenda");
        spanDeBorda.Should().NotBeNull(
            "setup do teste: precisa existir um span pai 'de borda' ativo para provar hierarquia");

        // Dentro do escopo do span pai (Activity.Current), inicia o span de camada —
        // exatamente o que AgendaService.GetAgendaAsync/AgendaReadRepository.GetByIntervaloAsync
        // fazem em produção (S3D-04b).
        using var spanDeCamada = KuraActivitySource.Instancia.StartActivity("Application.AgendaService.GetAgendaAsync");

        spanDeCamada.Should().NotBeNull(
            "KuraActivitySource precisa estar 'ouvido' pelo TracerProvider registrado — sem isso StartActivity devolve null por design");
        spanDeCamada!.ParentId.Should().Be(spanDeBorda!.Id,
            "S3D-04b: prova central da task — span-filho de camada precisa carregar ParentId apontando pro span HTTP pai");
        spanDeCamada.ParentSpanId.Should().Be(spanDeBorda.SpanId,
            "ParentSpanId é o campo que o Console exporter imprime como hierarquia real (achado do G2 da S3D-04)");
    }
}
