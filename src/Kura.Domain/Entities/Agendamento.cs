namespace Kura.Domain.Entities;

public class Agendamento
{
    public long Id { get; set; }
    public long? IdPet { get; set; }
    public long? IdVeterinario { get; set; }
    public string NmPaciente { get; set; } = string.Empty;
    public DateTime DtAgendamento { get; set; }
    public string DsServico { get; set; } = string.Empty;
    public string StStatus { get; set; } = string.Empty;
}
