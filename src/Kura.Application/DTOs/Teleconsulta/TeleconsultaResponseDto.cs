namespace Kura.Application.DTOs.Teleconsulta;

public sealed class TeleconsultaResponseDto
{
    public long IdAgendamento { get; init; }
    public string? DsSalaUrl { get; init; }
    public string? DsProvedorVideo { get; init; }
    public DateTime? DtInicioSessao { get; init; }
    public bool StFallbackManual { get; init; }
}
