namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Medicamento;

public sealed class MedicamentoCreateValidator : AbstractValidator<MedicamentoCreateDto>
{
    public MedicamentoCreateValidator()
    {
        RuleFor(x => x.NmMedicamento)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DsPrincipioAtivo)
            .NotEmpty()
            .MaximumLength(200);

        // TASK-60: DS_APRESENTACAO é NOT NULL no Oracle (MEDICAMENTO.DS_APRESENTACAO, V9:78,
        // migration imutável), mas de propósito sem NotEmpty() aqui — mesmo padrão da TASK-56
        // (DsObservacao em ConsultaCreateValidator): quem satisfaz a restrição de armazenamento
        // é o coalesce em MedicamentoService.CreateAsync, não uma regra de negócio no validator.
        RuleFor(x => x.DsApresentacao)
            .MaximumLength(500);
    }
}
