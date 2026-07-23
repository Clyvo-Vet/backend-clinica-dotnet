namespace Kura.Application.DTOs.Auth;

using Kura.Application.DTOs.Veterinario;

public sealed class TokenResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public VeterinarioResponseDto Usuario { get; init; } = null!;
}
