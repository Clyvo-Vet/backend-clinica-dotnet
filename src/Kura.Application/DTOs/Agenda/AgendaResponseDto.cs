namespace Kura.Application.DTOs.Agenda;

public class AgendaResponseDto
{
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public List<AgendamentoItemDto> Agendamentos { get; set; } = [];
}

public class AgendamentoItemDto
{
    public long IdAgendamento { get; set; }
    public DateTime DtAgendamento { get; set; }
    public int DuracaoMinutos { get; set; }
    public string NmTutor { get; set; } = string.Empty;
    public string NmPet { get; set; } = string.Empty;
    public long IdVeterinario { get; set; }
    public string NmVeterinario { get; set; } = string.Empty;
    public string DsTipoConsulta { get; set; } = string.Empty;
    public string DsStatus { get; set; } = string.Empty;
    public long NrVersion { get; set; }
}
