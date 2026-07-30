namespace Kura.Application.DTOs.Transcricao;

/// <summary>
/// Texto SOAP revisado pelo vet no momento da confirmação explícita
/// (PUT /eventos-clinicos/{id}/soap). Substitui o draft heurístico da Luna.
/// </summary>
public sealed class SoapConfirmarDto
{
    public string? S { get; init; }
    public string? O { get; init; }
    public string? A { get; init; }
    public string? P { get; init; }
}
