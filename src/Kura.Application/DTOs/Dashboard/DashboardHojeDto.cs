namespace Kura.Application.DTOs.Dashboard;

public sealed class DashboardHojeDto
{
    public int TotalConsultasHoje { get; init; }
    public int TotalAlertasAtivos { get; init; }
    public int TotalRetornosPendentes { get; init; }
    public List<PetResumoDto> UltimosPetsAtendidos { get; init; } = [];
    public List<AgendamentoResumoDto> ProximosAgendamentos { get; init; } = [];
}
