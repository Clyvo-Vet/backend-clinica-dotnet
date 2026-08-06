namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.EventoClinico;

public sealed class ConsultaCreateValidator : AbstractValidator<ConsultaCreateDto>
{
    public ConsultaCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.DtConsulta)
            .NotEmpty();

        RuleFor(x => x.DsMotivo)
            .NotEmpty()
            .MaximumLength(200);

        // TASK-47: EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL no Oracle — sem essa regra,
        // um payload sem DsObservacao passava pelo model binding (default "") e só
        // estourava ORA-01400 no INSERT, vazando 500 em vez de 400 de validação.
        RuleFor(x => x.DsObservacao)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
