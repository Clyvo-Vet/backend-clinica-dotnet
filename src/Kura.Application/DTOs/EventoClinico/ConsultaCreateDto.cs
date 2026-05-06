namespace Kura.Application.DTOs.EventoClinico;

public sealed class ConsultaCreateDto
{
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtConsulta { get; init; }
    public string DsMotivo { get; init; } = string.Empty;
    public string? DsAnamnese { get; init; }
    public string? DsExameFisico { get; init; }
    public string? DsDiagnostico { get; init; }
    public string? DsObservacao { get; init; }
}
