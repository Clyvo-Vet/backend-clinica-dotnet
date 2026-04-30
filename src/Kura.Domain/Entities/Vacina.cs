namespace Kura.Domain.Entities;

public class Vacina : EntidadeBase
{
    public long IdEventoClinico { get; set; }
    public string NmVacina { get; set; } = string.Empty;
    public string NrLote { get; set; } = string.Empty;
    public string DsFabricante { get; set; } = string.Empty;
    public DateTime? DtProximaDose { get; set; }
    public EventoClinico EventoClinico { get; set; } = null!;
}
