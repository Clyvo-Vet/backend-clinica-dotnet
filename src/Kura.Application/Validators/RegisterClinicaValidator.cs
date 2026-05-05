namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Auth;

public sealed class RegisterClinicaValidator : AbstractValidator<RegisterClinicaDto>
{
    public RegisterClinicaValidator()
    {
        RuleFor(x => x.NmClinica)
            .NotEmpty().WithMessage("Nome da clínica é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.NrCnpj)
            .NotEmpty().WithMessage("CNPJ é obrigatório.")
            .Matches(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$")
            .WithMessage("CNPJ inválido. Formato esperado: 00.000.000/0000-00.");

        RuleFor(x => x.DsEmailAcesso)
            .NotEmpty().WithMessage("Email de acesso é obrigatório.")
            .EmailAddress().WithMessage("Email de acesso inválido.");

        RuleFor(x => x.DsSenha)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");

        RuleFor(x => x.NrTelefone)
            .NotEmpty().WithMessage("Telefone é obrigatório.");
    }
}
