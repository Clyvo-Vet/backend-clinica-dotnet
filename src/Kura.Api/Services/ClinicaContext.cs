namespace Kura.Api.Services;

using Kura.Domain.Interfaces;

public class ClinicaContext : IClinicaContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClinicaContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long IdClinica => GetClaimValue("clinicaId");

    public long IdVeterinario => GetClaimValue("veterinarioId");

    private long GetClaimValue(string claimType)
    {
        var claim = _httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => c.Type == claimType);

        if (claim is null || !long.TryParse(claim.Value, out var value))
        {
            throw new UnauthorizedAccessException(
                $"Claim '{claimType}' ausente ou inválida no token JWT.");
        }

        return value;
    }
}
