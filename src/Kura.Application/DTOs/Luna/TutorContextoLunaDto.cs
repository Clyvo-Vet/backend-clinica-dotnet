namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Resposta de GET /api/v1/tutores/telefone/{numero} — shape espelha
/// TutorContextoDTO em kura-luna-ai/luna/src/integration/dtos.py (Pydantic,
/// snake_case). NÃO renomear/remover campos sem atualizar o DTO Python — são o
/// mesmo contrato visto dos dois lados.
/// </summary>
public sealed class TutorContextoLunaDto
{
    [JsonPropertyName("id_tutor")]
    public long IdTutor { get; init; }

    [JsonPropertyName("nm_tutor")]
    public string NmTutor { get; init; } = string.Empty;

    [JsonPropertyName("ds_whatsapp")]
    public string DsWhatsapp { get; init; } = string.Empty;

    [JsonPropertyName("id_clinica")]
    public long IdClinica { get; init; }

    [JsonPropertyName("pets")]
    public List<PetResumoLunaDto> Pets { get; init; } = [];
}
