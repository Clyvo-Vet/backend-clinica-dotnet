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

    /// <summary>
    /// FK nullable para INTERACAO_CANAL (TASK-66/67) — liga a triagem à interação de
    /// canal que a originou. TriageRequestDTO da Luna sempre envia id_interacao, mas a
    /// coluna nasceu nullable porque TRIAGEM_LUNA é pré-existente (V9).
    /// </summary>
    public long? IdInteracao { get; set; }
}
