namespace Kura.Domain.Entities;

public class Agendamento
{
    public long Id { get; set; }
    public long IdClinica { get; set; }
    public long? IdPet { get; set; }
    public long? IdTutor { get; set; }
    public long? IdVeterinario { get; set; }
    public string NmPaciente { get; set; } = string.Empty;    // keep for Dashboard
    public DateTime DtAgendamento { get; set; }
    public int NrDuracaoMinutos { get; set; }
    public string DsServico { get; set; } = string.Empty;     // keep for Dashboard
    public string DsTipoConsulta { get; set; } = string.Empty;
    public string StStatus { get; set; } = string.Empty;      // keep for Dashboard
    public string DsStatus { get; set; } = string.Empty;
    public string DsOrigem { get; set; } = string.Empty;
    public bool StAtiva { get; set; } = true;
    public long NrVersion { get; set; }

    public Pet? Pet { get; set; }
    public Tutor? Tutor { get; set; }
    public Veterinario? Veterinario { get; set; }
}
