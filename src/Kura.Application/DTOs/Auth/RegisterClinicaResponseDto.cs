namespace Kura.Application.DTOs.Auth;

public sealed class RegisterClinicaResponseDto
{
    public long IdClinica { get; init; }
    public string NmClinica { get; init; } = string.Empty;
    public string DsEmailAcesso { get; init; } = string.Empty;
    public DateTime DtCriacao { get; init; }
}
