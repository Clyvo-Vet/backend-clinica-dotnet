namespace Kura.Domain.Entities;

public class Medicamento : EntidadeBase
{
    public string NmMedicamento { get; set; } = string.Empty;
    public string DsPrincipioAtivo { get; set; } = string.Empty;
    public string DsApresentacao { get; set; } = string.Empty;
}
