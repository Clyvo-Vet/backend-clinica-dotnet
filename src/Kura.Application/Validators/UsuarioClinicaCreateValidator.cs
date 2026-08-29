namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.UsuarioClinica;
using Kura.Domain.Entities;

/// <summary>
/// FD-04 — validação de contrato de <c>POST /api/v1/usuarios-clinica</c>.
///
/// <para>Os limites de tamanho são copiados do <c>CREATE TABLE</c> da
/// <c>V17__usuario_clinica.sql</c>, não inferidos: <c>DS_EMAIL VARCHAR2(120)</c> e
/// <c>TP_PERFIL VARCHAR2(20)</c>. Sem eles, um e-mail de 200 caracteres viraria
/// <c>ORA-12899</c> — <c>500</c> de banco no lugar de <c>400</c> de contrato, que é a mesma
/// classe de defeito que a TASK-60 varreu neste repo.</para>
///
/// <para>⚠️ <b>Não há regra sobre <c>IdClinica</c> porque o DTO não tem esse campo</b> — a
/// clínica vem do JWT. Ver <see cref="UsuarioClinicaCreateDto"/>.</para>
/// </summary>
public sealed class UsuarioClinicaCreateValidator : AbstractValidator<UsuarioClinicaCreateDto>
{
    public UsuarioClinicaCreateValidator()
    {
        RuleFor(x => x.DsEmail)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(120);

        // Mesmo piso de 8 caracteres do RegisterClinicaValidator: a senha criada aqui entra
        // no MESMO fluxo de login, então um piso menor abriria por outra porta o que aquele
        // validator fecha.
        RuleFor(x => x.DsSenha)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");

        RuleFor(x => x.TpPerfil)
            .NotEmpty().WithMessage("Perfil é obrigatório.")
            .Must(SerPerfilConhecido)
            .WithMessage(
                $"Perfil deve ser {PerfisUsuarioClinica.Gestor} ou "
                + $"{PerfisUsuarioClinica.Veterinario}.");

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0).When(x => x.IdVeterinario.HasValue)
            .WithMessage("IdVeterinario deve ser positivo quando informado.");
    }

    internal static bool SerPerfilConhecido(string perfil) =>
        perfil?.Trim().ToUpperInvariant() is PerfisUsuarioClinica.Gestor
                                          or PerfisUsuarioClinica.Veterinario;
}
