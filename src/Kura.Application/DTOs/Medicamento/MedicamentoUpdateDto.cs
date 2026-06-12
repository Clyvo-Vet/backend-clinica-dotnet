namespace Kura.Application.DTOs.Medicamento;

using System.ComponentModel.DataAnnotations;

public sealed class MedicamentoUpdateDto
{
    [Required, MinLength(1)]
    public string NmMedicamento { get; init; } = string.Empty;
    [Required, MinLength(1)]
    public string DsPrincipioAtivo { get; init; } = string.Empty;
    [Required, MinLength(1)]
    public string DsApresentacao { get; init; } = string.Empty;
}
