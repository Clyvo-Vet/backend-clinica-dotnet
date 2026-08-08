namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Corpo de POST /api/v1/luna/triage — espelha TriageRequestDTO em
/// kura-luna-ai/luna/src/integration/dtos.py (Pydantic, snake_case).
///
/// sintomas[]/nr_score/ds_recomendacao não têm coluna própria em TRIAGEM_LUNA
/// (V9__schema_drift_clinico.sql) — LunaService compõe esses 3 campos dentro de
/// DS_DESCRICAO (VARCHAR2(2000)) em vez de pedir uma migration nova (V16) nesta
/// task. Ver decisão 2 no relatório da TASK-67.
/// </summary>
public sealed class TriageRequestDto
{
    [JsonPropertyName("id_interacao")]
    public long IdInteracao { get; init; }

    [JsonPropertyName("id_tutor")]
    public long IdTutor { get; init; }

    [JsonPropertyName("sintomas")]
    public List<string> Sintomas { get; init; } = [];

    [JsonPropertyName("ds_urgencia")]
    public string DsUrgencia { get; init; } = string.Empty;

    [JsonPropertyName("nr_score")]
    public int NrScore { get; init; }

    [JsonPropertyName("ds_recomendacao")]
    public string DsRecomendacao { get; init; } = string.Empty;
}
