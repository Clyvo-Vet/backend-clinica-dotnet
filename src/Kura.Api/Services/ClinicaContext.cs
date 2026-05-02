namespace Kura.Api.Services;

using Kura.Domain.Interfaces;

public class ClinicaContext : IClinicaContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClinicaContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long IdClinica => GetRequiredClaimValue("clinicaId");

    public long IdVeterinario => GetRequiredClaimValue("veterinarioId");

    public long? IdClinicaFiltro => TryGetClaimValue("clinicaId");

    private long GetRequiredClaimValue(string claimType)
    {
        var value = TryGetClaimValue(claimType);
        if (value is null)
            throw new UnauthorizedAccessException(
                $"Claim '{claimType}' ausente ou inválida no token JWT.");
        return value.Value;
    }

    private long? TryGetClaimValue(string claimType)
    {
        var claim = _httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => c.Type == claimType);
        return claim is not null && long.TryParse(claim.Value, out var value) ? value : null;
    }
}
