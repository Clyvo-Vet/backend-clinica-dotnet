namespace Kura.Application.DTOs.Luna;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Corpo de POST /api/v1/luna/interactions — espelha InteractionRequestDTO em
/// kura-luna-ai/luna/src/integration/dtos.py (Pydantic, snake_case).
/// id_tutor é nullable no contrato Python (int | None); ver LunaService para a
/// decisão de negócio quando ele vem null (não dá pra derivar ID_CLINICA).
/// </summary>
public sealed class InteractionRequestDto
{
    [JsonPropertyName("id_tutor")]
    public long? IdTutor { get; init; }

    [JsonPropertyName("ds_canal")]
    public string DsCanal { get; init; } = string.Empty;

    [JsonPropertyName("ds_direcao")]
    public string DsDirecao { get; init; } = string.Empty;

    [JsonPropertyName("ds_conteudo")]
    public string DsConteudo { get; init; } = string.Empty;

    [JsonPropertyName("dt_recebimento")]
    public DateTime DtRecebimento { get; init; }

    /// <summary>
    /// dict | None no Pydantic — capturado como JSON bruto (JsonElement) em vez de
    /// um tipo fortemente tipado porque o contrato Python não define shape para os
    /// metadados (ex.: IDs de mídia do WhatsApp). Persistido como texto JSON no CLOB
    /// DS_METADADOS, sem perda/normalização.
    /// </summary>
    [JsonPropertyName("ds_metadados")]
    public JsonElement? DsMetadados { get; init; }
}
