namespace Kura.Domain.Entities;

public class LeituraTemperatura : EntidadeBase
{
    public long IdDispositivoIot { get; set; }
    public decimal VlTemperatura { get; set; }
    public decimal? VlUmidade { get; set; }
    public DateTime DtLeitura { get; set; }
    public DispositivoIot DispositivoIot { get; set; } = null!;
}
