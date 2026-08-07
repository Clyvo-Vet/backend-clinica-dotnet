namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Prescricao;

public sealed class PrescricaoCreateValidator : AbstractValidator<PrescricaoCreateDto>
{
    public PrescricaoCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.IdMedicamento)
            .GreaterThan(0);

        RuleFor(x => x.DsPosologia)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.NrDuracaoDias)
            .InclusiveBetween(1, 365);

        // TASK-62 (mobile-clinica-rn, já pushed) adicionou um campo de observação sem
        // limite de tamanho no form de receituário. EVENTO_CLINICO.DS_OBSERVACAO é
        // VARCHAR2(1000) (EventoClinicoConfiguration.cs, HasMaxLength(1000)) — sem esta
        // regra, um texto colado com mais de 1000 caracteres chegava direto no Oracle e
        // estourava ORA-12899 (500) em vez de 400. Sem NotEmpty(): campo opcional desde a
        // TASK-56, mesmo padrão de ConsultaCreateValidator.
        RuleFor(x => x.DsObservacao)
            .MaximumLength(1000);
    }
}
