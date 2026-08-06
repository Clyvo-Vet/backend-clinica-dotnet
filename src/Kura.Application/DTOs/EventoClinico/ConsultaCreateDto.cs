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

    // TASK-47: coluna EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL no Oracle (Flyway V9).
    // Antes desse campo era nullable e um payload sem DsObservacao vazava ORA-01400
    // (banco trata '' como NULL) como 500 cru. Obrigatório aqui + validado em
    // ConsultaCreateValidator para virar 400 antes de chegar ao banco.
    public string DsObservacao { get; init; } = string.Empty;
}
