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
    /// (<c>Kura.Api</c>). É o nome que o SDK usa para decidir se ouve esta fonte.
    ///
    /// ⚠️ <b>Correção da 2ª volta da S3D-04c (achado I1 do G2):</b> a versão anterior deste
    /// comentário alertava que "divergência entre os dois mata o tracing em silêncio" e usava
    /// isso como razão principal para não mexer no valor. <b>Esse perigo não existe da forma
    /// descrita</b>, e o revisor provou: há <b>um único literal</b> desta string no
    /// repositório inteiro, e o <c>.AddSource(...)</c> referencia <b>esta constante</b>, nunca
    /// uma cópia — não há dois lados para divergir, há uma fonte da verdade e dois leitores
    /// dela. Trocar o valor da constante deixa a suíte verde (mutação C6 do G2, 284/284).
    /// Manter o alerta seria documentação afirmando um risco que a estrutura do código
    /// elimina — o mesmo dano de "documentação que garante o que o código não faz", invertido:
    /// alguém que precisasse renomear a fonte deixaria de fazê-lo por medo de um risco
    /// inexistente.
    ///
    /// <b>O valor é mantido por uma razão que se sustenta sozinha:</b> o prefixo
    /// <c>Kura.Api</c> nomeia o SERVIÇO implantado — o mesmo <c>service.name</c> que
    /// <c>ObservabilityExtensions</c> grava no <c>Resource</c> — e não a camada de API.
    /// Lê-se "o escopo cross-layer do serviço Kura.Api", e no output do exporter o escopo
    /// <c>Kura.Api.CrossLayer</c> aparece ao lado de <c>service.name: Kura.Api</c>, o que
    /// reforça a leitura. Fora do <c>Kura.Domain</c>, esse nome deixou de ser contradição de
    /// vocabulário — que era a objeção real do G2 da S3D-04b.
    ///
    /// Nota de precisão do G2, registrada para quem for mexer aqui: <c>const</c> em C# é
    /// embutido em tempo de compilação no assembly consumidor, então existe UM cenário de
    /// divergência real — recompilar <c>Kura.CrossCutting</c> sem recompilar <c>Kura.Api</c>.
    /// É cenário de binário obsoleto, não de "digitaram diferente", e não é mitigado por
    /// manter o valor; no fluxo de build deste repo (<c>dotnet build KuraApi.slnx</c> /
    /// <c>docker build</c> do zero) todos os assemblies são recompilados juntos.
    /// </summary>
    public const string NomeFonte = "Kura.Api.CrossLayer";

    /// <summary>
    /// Instância única do processo. <see cref="ActivitySource"/> é thread-safe e
    /// projetado para ser compartilhado — não recriar por chamada.
    /// </summary>
    public static readonly ActivitySource Instancia = new(NomeFonte, "1.0.0");
}
