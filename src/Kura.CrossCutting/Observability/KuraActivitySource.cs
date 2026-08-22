namespace Kura.CrossCutting.Observability;

using System.Diagnostics;

/// <summary>
/// S3D-04b: <see cref="ActivitySource"/> customizado para tracing ENTRE CAMADAS
/// (Application → Infrastructure), complementar ao span de borda HTTP que
/// <c>AddAspNetCoreInstrumentation()</c> (S3D-04) já produz.
///
/// Por que existe: o G2 da S3D-04 mediu que <c>AddAspNetCoreInstrumentation()</c> sozinho
/// produz um span PLANO por requisição — sem <c>Activity.ParentId</c>, sem hierarquia. A
/// alternativa óbvia (<c>OpenTelemetry.Instrumentation.EntityFrameworkCore</c>) não tem
/// release estável (toda a série publicada é prerelease, até <c>1.18.0-beta.1</c>) e, mesmo
/// se tivesse, produziria um span de COMANDO SQL — camada de dados, não fronteira
/// arquitetural. Um <see cref="ActivitySource"/> customizado é BCL pura (zero dependência,
/// disponível desde .NET 5) e permite nomear o span pela CAMADA do próprio projeto
/// (<c>Kura.Application</c>/<c>Kura.Infrastructure</c>), que é o que a rubrica pede de forma
/// mais literal.
///
/// S3D-04c: vive em <c>Kura.CrossCutting</c>, projeto criado nesta task exatamente para
/// hospedá-la. A S3D-04b tinha posto a classe em <c>Kura.Domain</c> — a justificativa era
/// mecanicamente correta (era o único projeto referenciado tanto por <c>Kura.Application</c>
/// quanto por <c>Kura.Infrastructure</c>, então evitava referência cruzada nova entre as duas
/// camadas), mas resolvia o grafo de dependência sem resolver a coerência conceitual: em
/// Clean Architecture o núcleo de domínio descreve regra de negócio e ignora que existam API,
/// banco ou telemetria. Tracing é <b>cross-cutting concern</b> por definição — nenhuma das
/// três camadas o "possui", ele atravessa todas. <c>Kura.CrossCutting</c> ocupa a mesma
/// posição no grafo que <c>Kura.Domain</c> ocupava (sem <c>ProjectReference</c> nenhuma,
/// referenciado por Application e Infrastructure), então a realocação não muda o
/// comportamento em runtime — só deixa de contaminar o núcleo.
/// </summary>
public static class KuraActivitySource
{
    /// <summary>
    /// Nome usado tanto na criação do <see cref="ActivitySource"/> quanto no
    /// <c>.AddSource(...)</c> registrado em <c>ObservabilityExtensions</c>
    /// (<c>Kura.Api</c>). Os dois precisam bater — é o nome que o SDK usa para decidir
    /// se ouve esta fonte. Divergência entre os dois mata o tracing EM SILÊNCIO
    /// (<c>StartActivity</c> passa a devolver <c>null</c>, sem erro), por isso o valor não
    /// foi alterado na S3D-04c junto com a mudança de projeto/namespace.
    ///
    /// O prefixo <c>Kura.Api</c> aqui nomeia o SERVIÇO implantado (o mesmo
    /// <c>service.name</c> configurado no <c>ResourceBuilder</c> pela S3D-04c), não a camada
    /// de API — lê-se "o escopo cross-layer do serviço Kura.Api". Fora do
    /// <c>Kura.Domain</c>, esse nome deixou de ser contradição de vocabulário.
    /// </summary>
    public const string NomeFonte = "Kura.Api.CrossLayer";

    /// <summary>
    /// Instância única do processo. <see cref="ActivitySource"/> é thread-safe e
    /// projetado para ser compartilhado — não recriar por chamada.
    /// </summary>
    public static readonly ActivitySource Instancia = new(NomeFonte, "1.0.0");
}
