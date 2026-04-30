namespace Kura.Domain.Entities;

public class Prescricao : EntidadeBase
{
    public long IdEventoClinico { get; set; }
    public long IdMedicamento { get; set; }
    public string DsPosologia { get; set; } = string.Empty;
    public int NrDuracaoDias { get; set; }
    public EventoClinico EventoClinico { get; set; } = null!;
    public Medicamento Medicamento { get; set; } = null!;
}
