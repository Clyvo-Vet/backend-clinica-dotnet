namespace Kura.Domain.Entities;

public class Documento : EntidadeBase
{
    public long IdEventoClinico { get; set; }
    public string NmArquivo { get; set; } = string.Empty;
    public string DsTipoMime { get; set; } = string.Empty;
    public string DsCaminho { get; set; } = string.Empty;
    public long NrTamanhoBytes { get; set; }
    public EventoClinico EventoClinico { get; set; } = null!;
}
