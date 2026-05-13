namespace Kura.Domain.Entities;

public abstract class EntidadeBase
{
    public long Id { get; set; }
    public bool StAtiva { get; set; } = true;
    public DateTime DtCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DtAtualizacao { get; set; }
}
