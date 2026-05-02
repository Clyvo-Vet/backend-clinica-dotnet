namespace Kura.Domain.Entities;

public class EventoClinico : EntidadeBase
{
    public long IdClinica { get; set; }
    public long IdPet { get; set; }
    public long IdVeterinario { get; set; }
    public long IdTipoEvento { get; set; }
    public DateTime DtEvento { get; set; }
    public string DsObservacao { get; set; } = string.Empty;
    public Pet Pet { get; set; } = null!;
    public Veterinario Veterinario { get; set; } = null!;
    public TipoEvento TipoEvento { get; set; } = null!;
}
