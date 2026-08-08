namespace Kura.Api.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Autenticação por API Key para os 3 endpoints consumidos pela IA Luna (TASK-67):
/// <c>GET /api/v1/tutores/telefone/{numero}</c>, <c>POST /api/v1/luna/interactions</c>,
/// <c>POST /api/v1/luna/triage</c>.
///
/// Sibling de <see cref="ApiKeyAuthFilter"/> em vez de generalizá-lo: os dois filtros
/// são pequenos e óbvios, cada um lê uma única chave de config (IoT:ApiKey aqui não
/// muda) e o <see cref="Kura.Api.Controllers.IotController"/> fica com zero risco de
/// regressão — nenhuma linha dele foi tocada nesta task. Generalizar (ex.: parametrizar
/// <c>ApiKeyAuthFilter</c> por config key via <c>TypeFilterAttribute</c>) traria
/// complexidade de DI (argumentos posicionais em vez de resolução automática) sem
/// benefício real para 2 consumidores.
///
/// CONTRATO DE AUTH (TASK-67 → TASK-68 lê isto): header <c>X-Api-Key</c>, config
/// <c>Luna:ApiKey</c> — env var <c>Luna__ApiKey</c>. Este par já estava documentado em
/// README.md/docker-compose.yml deste repo e em DevOps-Cloud/docker-compose.yml desde
/// a TASK-09 (INT-02), mas nunca tinha sido implementado do lado servidor (nada lia
/// "Luna:ApiKey" até este filtro) — os 3 endpoints não existiam. O valor já é injetado
/// hoje em ambos os serviços do DevOps-Cloud a partir da MESMA variável
/// <c>LUNA_API_KEY</c> (kura-api recebe via <c>Luna__ApiKey</c>; luna-ai recebe via
/// <c>KURA_API_KEY</c>, settings.py:24) — nenhuma mudança adicional necessária em
/// DevOps-Cloud para este par funcionar fim a fim.
///
/// NÃO reusar <c>Luna:InboundApiKey</c> (chave de saída .NET→Luna, FEAT-02/transcrição
/// — direção oposta) nem <c>IoT:ApiKey</c> (acoplamento indevido entre domínios
/// diferentes — ver <see cref="ApiKeyAuthFilter"/>).
/// </summary>
public class LunaApiKeyAuthFilter : IAuthorizationFilter
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly string _apiKey;

    public LunaApiKeyAuthFilter(IConfiguration configuration)
    {
        _apiKey = configuration["Luna:ApiKey"]
            ?? throw new InvalidOperationException("Luna:ApiKey not configured.");
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || providedKey != _apiKey)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
