namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.UsuarioClinica;
using Kura.Domain.Entities;

/// <summary>FD-04 — validação de <c>PUT /api/v1/usuarios-clinica/{id}</c>.</summary>
public sealed class UsuarioClinicaUpdateValidator : AbstractValidator<UsuarioClinicaUpdateDto>
{
    public UsuarioClinicaUpdateValidator()
    {
        RuleFor(x => x.DsEmail)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(120);

        RuleFor(x => x.TpPerfil)
            .NotEmpty().WithMessage("Perfil é obrigatório.")
            .Must(UsuarioClinicaCreateValidator.SerPerfilConhecido)
            .WithMessage(
                $"Perfil deve ser {PerfisUsuarioClinica.Gestor} ou "
                + $"{PerfisUsuarioClinica.Veterinario}.");

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0).When(x => x.IdVeterinario.HasValue)
            .WithMessage("IdVeterinario deve ser positivo quando informado.");
    }
}
