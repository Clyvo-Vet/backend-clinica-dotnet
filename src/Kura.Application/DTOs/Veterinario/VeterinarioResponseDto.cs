namespace Kura.Application.DTOs.Veterinario;

using System.Text.Json.Serialization;

public sealed class VeterinarioResponseDto
{
    public long Id { get; init; }
    public long IdClinica { get; init; }
    public string NmVeterinario { get; init; } = string.Empty;

    [JsonPropertyName("nrCRMV")]
    public string NrCrmv { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public bool StAtiva { get; init; }
}
