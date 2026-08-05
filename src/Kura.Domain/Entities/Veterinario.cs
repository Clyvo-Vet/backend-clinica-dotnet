namespace Kura.Domain.Entities;

public class Veterinario : EntidadeBase
{
    public long IdClinica { get; set; }
    public string NmVeterinario { get; set; } = string.Empty;
    public string NrCrmv { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;

    // Nullable: telefone é opcional no cadastro (ex.: admin criado via
    // register-clinica sem telefone). O Oracle físico (VETERINARIO.NR_TELEFONE,
    // V1__initial_schema.sql do repo Java) já é NULLABLE — forçar "" aqui só
    // criava uma garantia falsa, já que Oracle trata VARCHAR2 vazio como NULL
    // na escrita mesmo assim (ver TASK-36/E-4 em KURA_BACKLOG_FIX_2.md).
    public string? NrTelefone { get; set; }
    public Clinica Clinica { get; set; } = null!;
}
