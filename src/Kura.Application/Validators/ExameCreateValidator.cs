namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Exame;

public sealed class ExameCreateValidator : AbstractValidator<ExameCreateDto>
{
    public ExameCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.NmExame)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DsResultado)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(x => x.DtRealizacao)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("'DtRealizacao' não pode ser uma data futura.");

        // EVENTO_CLINICO.DS_OBSERVACAO é VARCHAR2(1000) (EventoClinicoConfiguration.cs,
        // HasMaxLength(1000)). Sem NotEmpty(): campo opcional desde a TASK-56, o coalesce
        // em ExameService satisfaz o NOT NULL do Oracle. Re-review escopado da revisão
        // final do FIX_4 (achado 3): faltava a mesma regra que Consulta/Prescricao já
        // tinham — sem ela, um dsObservacao > 1000 chars estourava ORA-12899 (500) em
        // vez de 400.
        RuleFor(x => x.DsObservacao)
            .MaximumLength(1000);
    }
}
