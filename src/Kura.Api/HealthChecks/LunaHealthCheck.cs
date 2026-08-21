namespace Kura.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// S3D-03: verifica a disponibilidade do serviço externo Luna (IA de triagem via
/// WhatsApp) chamando <c>GET /health</c> nela (<c>Luna:BaseUrl</c>, já usada por
/// <see cref="Kura.Application.Services.Interfaces.ILunaTranscricaoService"/> — ver
/// <c>ServiceCollectionExtensions.AddInfrastructure</c>).
///
/// Decisão travada no backlog (`KURA_BACKLOG_SPRINT3_DOTNET.md`, task `S3D-03`):
/// Luna fora do ar reporta <see cref="HealthStatus.Degraded"/>, NUNCA
/// <see cref="HealthStatus.Unhealthy"/>. A API da clínica continua 100% operacional
/// sem a Luna (só as duas FEATs que dependem dela — transcrição de áudio e triagem
/// por WhatsApp — ficam indisponíveis); marcar o serviço inteiro como doente por uma
/// dependência de terceiro fora do ar seria uma resposta falsa. Por isso este check
/// nunca lança e nunca devolve <c>Unhealthy</c> — todo caminho de falha (erro HTTP,
/// exceção de rede, timeout) devolve <c>Degraded</c> explicitamente.
///
/// Consequência de acoplamento verificada e provada no relatório da task: como
/// <c>Degraded</c> mapeia para HTTP 200 por padrão em <c>MapHealthChecks</c> (só
/// <c>Unhealthy</c> vira 503), o healthcheck do container <c>kura-api</c> no
/// <c>docker-compose.yml</c> (<c>curl -sf .../health</c>) continua passando com a
/// Luna fora do ar — o que preserva a cadeia <c>luna-ai: depends_on: kura-api:
/// condition: service_healthy</c> em vez de travá-la numa dependência circular de
/// disponibilidade.
/// </summary>
public sealed class LunaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LunaHealthCheck> _logger;

    public LunaHealthCheck(HttpClient httpClient, ILogger<LunaHealthCheck> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Luna respondeu 200 em /health.")
                : HealthCheckResult.Degraded(
                    $"Luna respondeu {(int)response.StatusCode} em /health — " +
                    "tratado como indisponibilidade de terceiro, não falha da API.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // TaskCanceledException cobre tanto o timeout curto do HttpClient (ver
            // registro em ServiceCollectionExtensions) quanto um cancellationToken
            // externo — nos dois casos, LGPD à parte (não há PII nesta chamada,
            // é só /health), tratamos como Luna indisponível, não como bug da API.
            _logger.LogWarning(ex, "Luna indisponível ou não respondeu a tempo em /health.");
            return HealthCheckResult.Degraded("Luna indisponível ou não respondeu a tempo.", ex);
        }
    }
}
