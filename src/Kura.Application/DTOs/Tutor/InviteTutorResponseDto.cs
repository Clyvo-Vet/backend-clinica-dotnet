namespace Kura.Application.DTOs.Tutor;

public sealed class InviteTutorResponseDto
{
    public long Id { get; init; }
    public Guid NrToken { get; init; }
    public DateTime DtExpiracao { get; init; }
    public string DsCanal { get; init; } = string.Empty;
    public bool StUtilizado { get; init; }
}
