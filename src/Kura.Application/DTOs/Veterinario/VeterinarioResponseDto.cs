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

    // TASK-36: nullable — telefone não informado agora é refletido como null
    // na resposta HTTP, não mascarado como "" (ver Kura.Domain.Entities.Veterinario).
    public string? NrTelefone { get; init; }
    public bool StAtiva { get; init; }
}
