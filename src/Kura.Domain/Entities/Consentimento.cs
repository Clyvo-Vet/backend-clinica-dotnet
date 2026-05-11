namespace Kura.Domain.Entities;

public class Consentimento
{
    public long Id { get; set; }
    public long IdTutor { get; set; }
    public string DsTipo { get; set; } = string.Empty;
    public char StAceito { get; set; }
    public string NrVersaoTermo { get; set; } = string.Empty;
    public DateTime DtConsentimento { get; set; }
}
