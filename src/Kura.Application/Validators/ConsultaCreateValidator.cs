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
    }
}
