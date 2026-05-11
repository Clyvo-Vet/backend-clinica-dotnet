namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Agenda;

public sealed class AtualizarStatusAgendamentoValidator : AbstractValidator<AtualizarStatusAgendamentoDto>
{
    public AtualizarStatusAgendamentoValidator()
    {
        RuleFor(x => x.DsStatus)
            .Must(s => s is "REALIZADO" or "CANCELADO")
            .WithMessage("'DsStatus' deve ser REALIZADO ou CANCELADO.");

        RuleFor(x => x.NrVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("'NrVersion' deve ser >= 0.");
    }
}
