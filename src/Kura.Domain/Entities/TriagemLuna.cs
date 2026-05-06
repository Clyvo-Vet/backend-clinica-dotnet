namespace Kura.Domain.Entities;

public class TriagemLuna : EntidadeBase
{
    public long IdClinica { get; set; }
    public long? IdTutor { get; set; }
    public long? IdPet { get; set; }
    public string DsNivelUrgencia { get; set; } = string.Empty;  // URGENTE | MODERADO | LEVE
    public string DsDescricao { get; set; } = string.Empty;
    public bool StEncaminhadoVet { get; set; }
    public DateTime DtTriagem { get; set; }
}
