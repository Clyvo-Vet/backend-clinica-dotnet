namespace Kura.Domain.Entities;

/// <summary>
/// Interação de canal (WhatsApp/e-mail/SMS) registrada pela IA Luna via
/// POST /api/v1/luna/interactions (TASK-67). Tabela .NET-owned criada pela
/// TASK-66 (backend-tutor-java, V15__interacao_canal.sql).
/// </summary>
public class InteracaoCanal : EntidadeBase
{
    public long IdClinica { get; set; }
    public long? IdTutor { get; set; }
    public string DsCanal { get; set; } = string.Empty;       // WHATSAPP | EMAIL | SMS
    public string DsDirecao { get; set; } = string.Empty;     // INBOUND | OUTBOUND
    public string DsConteudo { get; set; } = string.Empty;
    public DateTime DtRecebimento { get; set; }
    public string? DsMetadados { get; set; }                  // CLOB, JSON serializado
}
