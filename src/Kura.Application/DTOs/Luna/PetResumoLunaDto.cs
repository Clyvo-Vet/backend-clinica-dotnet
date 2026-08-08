namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Espelha PetResumoDTO em kura-luna-ai/luna/src/integration/dtos.py.
/// </summary>
public sealed class PetResumoLunaDto
{
    [JsonPropertyName("id_pet")]
    public long IdPet { get; init; }

    [JsonPropertyName("nm_pet")]
    public string NmPet { get; init; } = string.Empty;

    [JsonPropertyName("nm_especie")]
    public string NmEspecie { get; init; } = string.Empty;

    [JsonPropertyName("nm_raca")]
    public string? NmRaca { get; init; }
}
