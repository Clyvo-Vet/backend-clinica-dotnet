namespace Kura.Application.DTOs.Luna;

public class RelatorioTriagensDto
{
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public int TotalTriagens { get; set; }
    public Dictionary<string, int> PorUrgencia { get; set; } = new();
    public int EncaminhadasParaVet { get; set; }
}
