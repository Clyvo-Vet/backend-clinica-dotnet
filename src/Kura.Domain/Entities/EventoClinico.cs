namespace Kura.Domain.Entities;

public class EventoClinico : EntidadeBase
{
    public long IdClinica { get; set; }
    public long IdPet { get; set; }
    public long IdVeterinario { get; set; }
    public long IdTipoEvento { get; set; }
    public DateTime DtEvento { get; set; }
    public string DsObservacao { get; set; } = string.Empty;
    public string? DsTranscricao { get; set; }
    public string? DsSoapS { get; set; }
    public string? DsSoapO { get; set; }
    public string? DsSoapA { get; set; }
    public string? DsSoapP { get; set; }
    public bool StSoapConfirmado { get; set; }
    public Pet Pet { get; set; } = null!;
    public Veterinario Veterinario { get; set; } = null!;
    public TipoEvento TipoEvento { get; set; } = null!;
}
