namespace Kura.Domain.Entities;

public class ContaTutor
{
    public long Id { get; set; }
    public long IdTutor { get; set; }
    public string DsEmail { get; set; } = string.Empty;
    public char StEmailVerificado { get; set; }
    public DateTime DtCadastro { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
