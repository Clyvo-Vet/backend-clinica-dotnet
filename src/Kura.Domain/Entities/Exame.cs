namespace Kura.Domain.Entities;

public class Exame : EntidadeBase
{
    public long IdEventoClinico { get; set; }
    public string NmExame { get; set; } = string.Empty;
    public string DsResultado { get; set; } = string.Empty;
    public DateTime DtRealizacao { get; set; }
    public EventoClinico EventoClinico { get; set; } = null!;
}
