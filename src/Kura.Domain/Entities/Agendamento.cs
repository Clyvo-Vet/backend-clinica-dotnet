namespace Kura.Domain.Entities;

public class Agendamento
{
    public long Id { get; set; }
    public long IdClinica { get; set; }
    public long? IdPet { get; set; }
    public long? IdTutor { get; set; }
    public long? IdVeterinario { get; set; }
    public string NmPaciente { get; set; } = string.Empty;
    public DateTime DtAgendamento { get; set; }
    public int NrDuracaoMinutos { get; set; }
    public string DsServico { get; set; } = string.Empty;
    public string DsTipoConsulta { get; set; } = string.Empty;
    public string StStatus { get; set; } = string.Empty;
    public string DsOrigem { get; set; } = string.Empty;
    public bool StAtiva { get; set; } = true;
    public long NrVersion { get; set; }
    // Flyway V5 columns
    public string? DsObservacoes { get; set; }
    public DateTime? DtCriacao { get; set; }
    public DateTime? DtConfirmacao { get; set; }
    public DateTime? DtCancelamento { get; set; }
    public string? DsMotivoCancel { get; set; }
    public long? IdEventoGerado { get; set; }

    public Pet? Pet { get; set; }
    public Tutor? Tutor { get; set; }
    public Veterinario? Veterinario { get; set; }
}
