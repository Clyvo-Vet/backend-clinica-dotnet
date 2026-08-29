namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.UsuarioClinica;

/// <summary>FD-04 — validação de <c>PUT /api/v1/usuarios-clinica/{id}/senha</c>.</summary>
public sealed class UsuarioClinicaSenhaUpdateValidator
    : AbstractValidator<UsuarioClinicaSenhaUpdateDto>
{
    public UsuarioClinicaSenhaUpdateValidator()
    {
        RuleFor(x => x.DsSenha)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");
    }
}
