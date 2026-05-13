namespace Kura.Application.DTOs.Clinica;

using Kura.Application.DTOs.Veterinario;

public sealed class ClinicaResponseDto
{
    public long Id { get; init; }
    public string NmClinica { get; init; } = string.Empty;
    public string NrCnpj { get; init; } = string.Empty;
    public string DsEndereco { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public bool StAtiva { get; init; }
    public IReadOnlyList<VeterinarioResponseDto> Veterinarios { get; init; } = [];
}
