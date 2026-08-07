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

        // TASK-56: reverte parcialmente a TASK-47, de propósito. A TASK-47 acertou o
        // diagnóstico (500 vazando por ORA-01400 quando DsObservacao vinha vazio) mas
        // errou o fix, transformando uma restrição de armazenamento (NOT NULL no Oracle)
        // em regra de negócio (NotEmpty() aqui). O form SOAP do app
        // (mobile-clinica-rn/.../consulta/[idPet].tsx) exige apenas um dos quatro campos
        // S/O/A/P preenchido — um vet pode legitimamente deixar "Plano" (DsObservacao)
        // vazio, e o cliente real não honra o NotEmpty(). Quem satisfaz o NOT NULL do
        // Oracle agora é o coalesce em ConsultaService (sentinela "Sem observações"),
        // onde a responsabilidade de persistência pertence.
        RuleFor(x => x.DsObservacao)
            .MaximumLength(1000);
    }
}
