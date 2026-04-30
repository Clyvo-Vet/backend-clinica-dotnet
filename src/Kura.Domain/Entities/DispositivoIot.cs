namespace Kura.Domain.Entities;

public class DispositivoIot : EntidadeBase
{
    public long IdClinica { get; set; }
    public string CdDispositivo { get; set; } = string.Empty;
    public string DsDescricao { get; set; } = string.Empty;
    public string DsLocalizacao { get; set; } = string.Empty;
    public Clinica Clinica { get; set; } = null!;
    public ICollection<LeituraTemperatura> Leituras { get; set; } = [];
}
