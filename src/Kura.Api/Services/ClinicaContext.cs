namespace Kura.Api.Services;

using Kura.Domain.Interfaces;

public class ClinicaContext : IClinicaContext
{
    /// <summary>Claim de papel emitida por <c>AuthService.GenerateToken</c> (FD-03).</summary>
    public const string ClaimPerfil = "perfil";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClinicaContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long IdClinica => GetRequiredClaimValue("clinicaId");

    // FD-03: era GetRequiredClaimValue (que LANÇA se a claim faltar). Um GESTOR não
    // veterinário recebe token SEM a claim `veterinarioId`, então "ausente" passou a ser
    // um estado legítimo — não um token malformado. Ver IClinicaContext.IdVeterinario para
    // a medição que mostra que nenhum consumidor dependia do comportamento antigo.
    public long? IdVeterinario => TryGetClaimValue("veterinarioId");

    public long? IdClinicaFiltro => TryGetClaimValue("clinicaId");

    public string? Perfil => TryGetClaimString(ClaimPerfil);

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
        var valor = TryGetClaimString(claimType);
        return valor is not null && long.TryParse(valor, out var value) ? value : null;
    }

    private string? TryGetClaimString(string claimType) =>
        _httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => c.Type == claimType)?.Value;
}
