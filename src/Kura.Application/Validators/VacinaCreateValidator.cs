namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Vacina;

public sealed class VacinaCreateValidator : AbstractValidator<VacinaCreateDto>
{
    public VacinaCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.DtEvento)
            .NotEmpty();

        RuleFor(x => x.NmVacina)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrLote)
            .NotEmpty()
            .MaximumLength(50);

        // TASK-60: DS_FABRICANTE é NOT NULL no Oracle (VACINA.DS_FABRICANTE, V9:170, migration
        // imutável), mas de propósito sem NotEmpty() aqui — mesmo padrão da TASK-56
        // (DsObservacao em ConsultaCreateValidator): quem satisfaz a restrição de armazenamento
        // é o coalesce em VacinaService.CreateAsync, não uma regra de negócio no validator.
        RuleFor(x => x.DsFabricante)
            .MaximumLength(200);
    }
}
