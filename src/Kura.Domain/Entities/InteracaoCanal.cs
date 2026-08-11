namespace Kura.Domain.Entities;

/// <summary>
/// Interação de canal (WhatsApp/e-mail/SMS) registrada pela IA Luna via
/// POST /api/v1/luna/interactions (TASK-67). Tabela .NET-owned criada pela
/// TASK-66 (backend-tutor-java, V15__interacao_canal.sql).
///
/// IdClinica é nullable desde a TASK-77 (FIX_7): decisão de produto do Felipe —
/// telefone não cadastrado (id_tutor null no payload da Luna) passa a GRAVAR a
/// interação em vez de ser rejeitado com 422, com ID_CLINICA/ID_TUTOR nulos.
/// Coluna nullable no Oracle desde V16__interacao_canal_clinica_nullable.sql
/// (backend-tutor-java, TASK-76). Ver LunaService.RegistrarInteracaoAsync para a
/// decisão completa e KuraDbContext.ApplyTenantFilters para a consequência no
/// filtro de tenant (linha com clínica nula fica invisível a leitura escopada).
/// </summary>
public class InteracaoCanal : EntidadeBase
{
    public long? IdClinica { get; set; }
    public long? IdTutor { get; set; }
    public string DsCanal { get; set; } = string.Empty;       // WHATSAPP | EMAIL | SMS
    public string DsDirecao { get; set; } = string.Empty;     // INBOUND | OUTBOUND
    public string DsConteudo { get; set; } = string.Empty;
    public DateTime DtRecebimento { get; set; }
    public string? DsMetadados { get; set; }                  // CLOB, JSON serializado
}
