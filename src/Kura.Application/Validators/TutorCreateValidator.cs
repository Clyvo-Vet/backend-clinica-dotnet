namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Tutor;

public sealed class TutorCreateValidator : AbstractValidator<TutorCreateDto>
{
    public TutorCreateValidator()
    {
        RuleFor(x => x.NmTutor)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrCpf)
            .NotEmpty()
            .Length(11)
            .Matches("^[0-9]{11}$").WithMessage("'NrCpf' deve conter exatamente 11 dígitos numéricos.");

        RuleFor(x => x.DsEmail)
            .NotEmpty()
            .MaximumLength(150);

        // TASK-60: DS_TELEFONE é NOT NULL no Oracle (TUTOR.DS_TELEFONE, V1:91, migration
        // imutável), mas de propósito sem NotEmpty() aqui — mesmo padrão da TASK-56
        // (DsObservacao em ConsultaCreateValidator): quem satisfaz a restrição de armazenamento
        // é o coalesce em TutorService.CreateAsync, não uma regra de negócio no validator.
        RuleFor(x => x.NrTelefone)
            .MaximumLength(20);

        RuleFor(x => x.DsCanalConvite)
            .Must(c => c is "WHATSAPP" or "EMAIL" or "SMS")
            .WithMessage("'DsCanalConvite' deve ser WHATSAPP, EMAIL ou SMS.");
    }
}
