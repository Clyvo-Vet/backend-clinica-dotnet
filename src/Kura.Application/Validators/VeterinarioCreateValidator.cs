namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Veterinario;

/// <summary>
/// ⚠️ <b>FD-05: a regra <c>RuleFor(x =&gt; x.IdClinica).GreaterThan(0)</c> foi REMOVIDA junto
/// com o campo.</b> Não é limpeza opcional: <c>VeterinarioCreateDto</c> deixou de ter
/// <c>IdClinica</c> (a clínica passou a sair do JWT — ver a documentação daquele DTO), e uma
/// <c>RuleFor</c> sobre propriedade inexistente não compila. Se alguém reintroduzir o campo,
/// reintroduzirá também esta linha — e é exatamente isso que a FD-05 existe para impedir.
/// </summary>
public sealed class VeterinarioCreateValidator : AbstractValidator<VeterinarioCreateDto>
{
    public VeterinarioCreateValidator()
    {
        RuleFor(x => x.NmVeterinario)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrCrmv)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.DsEmail)
            .NotEmpty()
            .MaximumLength(150);
    }
}
