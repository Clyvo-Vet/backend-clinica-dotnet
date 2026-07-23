namespace Kura.Application.DTOs.Auth;

using Kura.Application.DTOs.Veterinario;

public sealed class RegisterClinicaResponseDto
{
    public long IdClinica { get; init; }
    public string NmClinica { get; init; } = string.Empty;
    public string DsEmailAcesso { get; init; } = string.Empty;
    public DateTime DtCriacao { get; init; }
    public long IdVeterinarioAdmin { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public VeterinarioResponseDto Usuario { get; init; } = null!;
}
