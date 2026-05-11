namespace Kura.Application.DTOs.Tutor;

public sealed class TutorComInviteResponseDto
{
    public long Id { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public char StAtiva { get; init; }
    public InviteTutorResponseDto Invite { get; init; } = null!;
}
