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

    // TASK-47/TASK-56: coluna EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL no Oracle (Flyway V9).
    // A TASK-47 tornou o campo obrigatório no contrato (NotEmpty() no validator), mas a
    // TASK-56 reverteu isso de propósito: o form SOAP do app exige apenas um dos quatro
    // campos S/O/A/P preenchido, então "Plano" (DsObservacao) vazio é um caso legítimo do
    // cliente. Hoje o campo é opcional do ponto de vista do contrato — quem satisfaz o
    // NOT NULL do Oracle é o coalesce em ConsultaService (sentinela "Sem observações");
    // ConsultaCreateValidator só valida o tamanho máximo (MaximumLength(1000)).
    public string DsObservacao { get; init; } = string.Empty;
}
