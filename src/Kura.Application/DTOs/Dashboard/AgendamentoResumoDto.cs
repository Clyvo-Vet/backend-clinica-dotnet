namespace Kura.Application.DTOs.Dashboard;

public sealed class AgendamentoResumoDto
{
    public long Id { get; init; }
    public string NmPaciente { get; init; } = string.Empty;
    public DateTime DtAgendamento { get; init; }
    public string DsServico { get; init; } = string.Empty;
    public string StStatus { get; init; } = string.Empty;
}
