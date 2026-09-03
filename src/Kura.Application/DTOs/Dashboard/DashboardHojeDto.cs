namespace Kura.Application.DTOs.Dashboard;

public sealed class DashboardHojeDto
{
    public int TotalConsultasHoje { get; init; }
    public int TotalAlertasAtivos { get; init; }
    public int TotalRetornosPendentes { get; init; }

    /// <summary>
    /// FD-17 — pets distintos com evento clínico hoje, <b>sem teto</b>. Diferente de
    /// <see cref="UltimosPetsAtendidos"/> (que satura em 5 por design, "últimos pets"): o app
    /// exibia <c>ultimosPetsAtendidos.length</c> rotulado como "Pacientes atendidos" ao lado de
    /// "Consultas hoje" — número que nunca passava de 5 e não falava do dia. Este campo é o
    /// contador honesto; <see cref="UltimosPetsAtendidos"/> continua existindo, intacto.
    /// </summary>
    public int TotalPacientesAtendidosHoje { get; init; }

    /// <summary>
    /// FD-17 — agendamentos de teleconsulta com sessão iniciada hoje, escopados por clínica.
    /// Antes hardcoded em zero no consumidor (não havia produtor identificado à primeira
    /// vista — o produtor real é <c>TeleconsultaService.CriarOuObterSalaAsync</c>, que escreve
    /// em <c>AGENDAMENTO</c>, não em <c>EVENTO_CLINICO</c>).
    /// </summary>
    public int TotalTeleorientacoesHoje { get; init; }

    public List<PetResumoDto> UltimosPetsAtendidos { get; init; } = [];
    public List<AgendamentoResumoDto> ProximosAgendamentos { get; init; } = [];
}
