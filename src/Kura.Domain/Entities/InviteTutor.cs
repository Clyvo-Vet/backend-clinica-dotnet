namespace Kura.Domain.Entities;

public class InviteTutor : EntidadeBase
{
    public long IdTutor { get; set; }
    public Guid NrToken { get; set; }
    public DateTime DtExpiracao { get; set; }
    public string DsCanal { get; set; } = "WHATSAPP";
    public bool StUtilizado { get; set; } = false;
    public Tutor Tutor { get; set; } = null!;
}
