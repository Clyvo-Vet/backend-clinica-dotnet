namespace Kura.Domain.Entities;

public class AlertaTemperatura : EntidadeBase
{
    public long IdLeituraTemperatura { get; set; }
    public string DsTipoAlerta { get; set; } = string.Empty;
    public decimal VlLimite { get; set; }
    public string DsMensagem { get; set; } = string.Empty;
    public char StResolvido { get; set; } = 'N';
    public LeituraTemperatura LeituraTemperatura { get; set; } = null!;
}
