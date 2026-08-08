namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Resposta de POST /api/v1/luna/triage — espelha TriageResponseDTO (Pydantic,
/// snake_case). kura_client.py lê resp.json()["id_triagem"] direto.
/// </summary>
public sealed class TriageResponseDto
{
    [JsonPropertyName("id_triagem")]
    public long IdTriagem { get; init; }
}
