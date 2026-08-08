namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Resposta de POST /api/v1/luna/interactions — espelha InteractionResponseDTO
/// (Pydantic, snake_case). kura_client.py lê resp.json()["id_interacao"] direto.
/// </summary>
public sealed class InteractionResponseDto
{
    [JsonPropertyName("id_interacao")]
    public long IdInteracao { get; init; }
}
