namespace Kura.Domain.Observability;

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
/// Vive em <c>Kura.Domain</c> por ser o único projeto referenciado tanto por
/// <c>Kura.Application</c> quanto por <c>Kura.Infrastructure</c> — evita referência cruzada
/// nova entre as duas camadas só para compartilhar esta fonte de tracing.
/// </summary>
public static class KuraActivitySource
{
    /// <summary>
    /// Nome usado tanto na criação do <see cref="ActivitySource"/> quanto no
    /// <c>.AddSource(...)</c> registrado em <c>ObservabilityExtensions</c>
    /// (<c>Kura.Api</c>). Os dois precisam bater — é o nome que o SDK usa para decidir
    /// se ouve esta fonte.
    /// </summary>
    public const string NomeFonte = "Kura.Api.CrossLayer";

    /// <summary>
    /// Instância única do processo. <see cref="ActivitySource"/> é thread-safe e
    /// projetado para ser compartilhado — não recriar por chamada.
    /// </summary>
    public static readonly ActivitySource Instancia = new(NomeFonte, "1.0.0");
}
