namespace Kura.Infrastructure.Tests;

using System.Diagnostics;
using FluentAssertions;
using Kura.Api.Extensions;
using Kura.Application.Services;
using Kura.CrossCutting.Observability;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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
///
/// 🆕 S3D-04c — o terceiro teste desta classe fecha o Important #1 do G2 da S3D-04b. O G2
/// provou por mutação que apagar as chamadas <c>StartActivity</c> de PRODUÇÃO
/// (<c>AgendaService.GetAgendaAsync</c> e <c>AgendaReadRepository.GetByIntervaloAsync</c>)
/// não derrubava nenhum dos 283 testes: os dois primeiros testes desta classe provam que o
/// SDK do .NET preenche <c>ParentId</c> quando ALGUÉM chama <c>StartActivity</c>, mas nunca
/// invocam o código instrumentado. O teste novo invoca o <c>AgendaService</c> REAL, com
/// <c>AgendaReadRepository</c> REAL sobre um <c>KuraDbContext</c> InMemory, e afirma sobre as
/// <see cref="Activity"/> que a PRODUÇÃO emitiu — então ele falha se aquelas linhas sumirem.
///
/// Por que <see cref="ActivityListener"/> próprio em vez de <c>AddKuraObservability()</c>:
/// o único exporter registrado em produção é o Console, que escreve em stdout e não devolve
/// os spans para inspeção. Capturar exigiria o pacote <c>OpenTelemetry.Exporter.InMemory</c>
/// (dependência nova só para teste); <see cref="ActivityListener"/> é BCL pura e observa
/// exatamente as mesmas <see cref="Activity"/> que o pipeline OTel observaria. O elo
/// "<c>KuraActivitySource</c> está registrado no <c>TracerProvider</c> de produção" continua
/// coberto pelo segundo teste desta classe — os dois se complementam, nenhum substitui o
/// outro.
/// </summary>
public class ObservabilityExtensionsTests
{
    private const string NomeFonteDeBordaSimulada = "Kura.Testes.SpanDeBorda";
    private const long IdClinicaDoTeste = 100L;

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

    /// <summary>
    /// S3D-04c item 1 — prova que a INSTRUMENTAÇÃO DE PRODUÇÃO existe e produz hierarquia,
    /// não que o SDK do .NET funciona. Chama <see cref="AgendaService"/> real (construtor
    /// real) com <see cref="AgendaReadRepository"/> real sobre <c>KuraDbContext</c> InMemory,
    /// dentro de um span pai, e afirma sobre as <see cref="Activity"/> realmente emitidas.
    ///
    /// MORDIDA (documentada em task-S3D-04c-report.md): comentar a linha
    /// <c>KuraActivitySource.Instancia.StartActivity(...)</c> em QUALQUER um dos dois
    /// arquivos de produção derruba este teste. Era exatamente essa mutação que os 283
    /// testes anteriores sobreviviam em silêncio.
    /// </summary>
    [Fact]
    public async Task AgendaServiceReal_ExecutadoDentroDeSpanDeBorda_EmiteSpansDeApplicationEInfrastructureComHierarquiaReal()
    {
        var spansCapturados = new List<Activity>();

        using var fonteDeBordaSimulada = new ActivitySource(NomeFonteDeBordaSimulada);
        using var listener = new ActivityListener
        {
            // Ouve a fonte de PRODUÇÃO (KuraActivitySource) e a fonte que simula o span de
            // borda HTTP. Não usa o TracerProvider do DI porque o exporter de produção é o
            // Console — ver doc da classe.
            ShouldListenTo = fonte =>
                fonte.Name == KuraActivitySource.NomeFonte || fonte.Name == NomeFonteDeBordaSimulada,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = atividade =>
            {
                lock (spansCapturados) spansCapturados.Add(atividade);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var contexto = CriarContextoInMemory();
        var dataInicio = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var dataFim = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        contexto.Agendamentos.Add(new Agendamento
        {
            Id = 1,
            IdClinica = IdClinicaDoTeste,
            NmPaciente = "Rex",
            DtAgendamento = dataInicio.AddDays(5),
            StStatus = "AGENDADO"
        });
        await contexto.SaveChangesAsync();

        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinica).Returns(IdClinicaDoTeste);

        // Construtor REAL do service de produção, com o repositório REAL de produção.
        var service = new AgendaService(
            new AgendaReadRepository(contexto),
            clinicaContext.Object,
            Mock.Of<IAgendamentoRepository>(),
            Mock.Of<IUnitOfWork>());

        using var spanDeBorda = fonteDeBordaSimulada.StartActivity("GET /api/v1/agenda", ActivityKind.Server);
        spanDeBorda.Should().NotBeNull(
            "setup do teste: precisa existir um span pai de borda ativo para provar hierarquia");

        var resposta = await service.GetAgendaAsync(dataInicio, dataFim, idVeterinario: null);

        // Garante que o fluxo real rodou de verdade (chegou ao repositório e voltou com
        // dado do banco) — sem isto o teste poderia passar com um service que nunca
        // consultou nada.
        resposta.Agendamentos.Should().HaveCount(1,
            "o fluxo real precisa ter atravessado Application -> Infrastructure -> DbContext");

        var spansDoTrace = spansCapturados
            .Where(a => a.TraceId == spanDeBorda!.TraceId)
            .ToList();

        var spanApplication = spansDoTrace
            .SingleOrDefault(a => a.OperationName == "Application.AgendaService.GetAgendaAsync");
        var spanInfrastructure = spansDoTrace
            .SingleOrDefault(a => a.OperationName == "Infrastructure.AgendaReadRepository.GetByIntervaloAsync");

        spanApplication.Should().NotBeNull(
            "AgendaService.GetAgendaAsync precisa abrir uma Activity de camada Application — se esta linha de produção sumir, o tracing entre camadas morre em silêncio (S3D-04b/S3D-04c)");
        spanInfrastructure.Should().NotBeNull(
            "AgendaReadRepository.GetByIntervaloAsync precisa abrir uma Activity de camada Infrastructure — mesma classe de regressão silenciosa");

        // Hierarquia: borda HTTP -> Application -> Infrastructure.
        spanApplication!.ParentSpanId.Should().Be(spanDeBorda!.SpanId,
            "o span de Application tem que ser filho do span de borda, por Activity.Current");
        spanInfrastructure!.ParentSpanId.Should().Be(spanApplication.SpanId,
            "prova central da S3D-04b: o span de Infrastructure é filho do de Application, não um irmão plano");
        spanInfrastructure.TraceId.Should().Be(spanApplication.TraceId,
            "os três spans pertencem ao mesmo trace");

        // Tags de camada — parte da mesma instrumentação de produção.
        spanApplication.GetTagItem("kura.layer").Should().Be("Application");
        spanInfrastructure.GetTagItem("kura.layer").Should().Be("Infrastructure");
        spanInfrastructure.GetTagItem("kura.id_clinica").Should().Be(IdClinicaDoTeste);
    }

    /// <summary>
    /// Mesmo padrão de <c>AgendamentoRepositoryTests.CreateContext</c>: InMemory, sem Oracle.
    /// <c>IdClinicaFiltro</c> nulo desliga os query filters de tenant (ver
    /// <c>KuraDbContext.ApplyTenantFilters</c>) — o escopo por clínica neste fluxo vem do
    /// <c>WHERE IdClinica</c> explícito do próprio <see cref="AgendaReadRepository"/>.
    /// </summary>
    private static KuraDbContext CriarContextoInMemory()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    /// <summary>
    /// S3D-04c item C2 — o teste que faltava, e que deixou a regressão C1 passar verde.
    /// </summary>
    [Fact]
    public async Task PipelineDeProducaoReal_SpansEmitidosPeloTracerProviderDoDI_PreservamHierarquiaEntreCamadasECarregamServiceName()
    {
        var coletor = new ColetorDeSpansDoProvider();

        var services = new ServiceCollection();
        services.AddKuraObservability();
        services.ConfigureOpenTelemetryTracerProvider(b => b.AddProcessor(coletor));

        using var sp = services.BuildServiceProvider();
        // Ordem deliberada: o TelemetryHostedService do OpenTelemetry.Extensions.Hosting
        // resolve MeterProvider antes de TracerProvider em produção — reproduzir a ordem
        // evita que o teste dependa de uma sequência que produção não usa.
        using var meterProvider = sp.GetRequiredService<MeterProvider>();
        using var tracerProvider = sp.GetRequiredService<TracerProvider>();

        var contexto = CriarContextoInMemory();
        var dataInicio = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var dataFim = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        contexto.Agendamentos.Add(new Agendamento
        {
            Id = 1, IdClinica = IdClinicaDoTeste, NmPaciente = "Rex",
            DtAgendamento = dataInicio.AddDays(5), StStatus = "AGENDADO"
        });
        await contexto.SaveChangesAsync();

        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinica).Returns(IdClinicaDoTeste);

        var service = new AgendaService(
            new AgendaReadRepository(contexto),
            clinicaContext.Object,
            Mock.Of<IAgendamentoRepository>(),
            Mock.Of<IUnitOfWork>());

        using var fonteHttpSimulada = new ActivitySource("Microsoft.AspNetCore");
        using var spanDeBorda = fonteHttpSimulada.StartActivity("GET api/v1/agenda", ActivityKind.Server);
        spanDeBorda.Should().NotBeNull();

        var resposta = await service.GetAgendaAsync(dataInicio, dataFim, idVeterinario: null);
        resposta.Agendamentos.Should().HaveCount(1);

        var spanApplication = coletor.Spans.SingleOrDefault(a => a.OperationName == "Application.AgendaService.GetAgendaAsync");
        var spanInfrastructure = coletor.Spans.SingleOrDefault(a => a.OperationName == "Infrastructure.AgendaReadRepository.GetByIntervaloAsync");

        spanApplication.Should().NotBeNull("DIAG: spans coletados = " + string.Join(", ", coletor.Spans.Select(a => a.OperationName)));
        spanInfrastructure.Should().NotBeNull("DIAG");
        spanInfrastructure!.ParentSpanId.Should().Be(spanApplication!.SpanId, "DIAG hierarquia");
        spanInfrastructure.TraceId.Should().Be(spanApplication.TraceId, "DIAG traceid");
        spanApplication.ParentSpanId.Should().Be(spanDeBorda!.SpanId, "DIAG borda");

        coletor.RecursoDoProvider.Should().NotBeNull();
        coletor.RecursoDoProvider!.Attributes.Should().Contain(a => a.Key == "service.name" && (string)a.Value == "Kura.Api");
    }

    private sealed class ColetorDeSpansDoProvider : BaseProcessor<Activity>
    {
        private readonly List<Activity> _spans = new();
        public IReadOnlyList<Activity> Spans { get { lock (_spans) return _spans.ToList(); } }
        public Resource? RecursoDoProvider { get; private set; }

        public override void OnEnd(Activity data)
        {
            RecursoDoProvider ??= ParentProvider?.GetResource();
            lock (_spans) _spans.Add(data);
        }
    }
}
