namespace Kura.Api.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public class ApiKeyAuthFilter : IAuthorizationFilter
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly string _apiKey;

    public ApiKeyAuthFilter(IConfiguration configuration)
    {
        _apiKey = configuration["IoT:ApiKey"]
            ?? throw new InvalidOperationException("IoT:ApiKey not configured.");
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
