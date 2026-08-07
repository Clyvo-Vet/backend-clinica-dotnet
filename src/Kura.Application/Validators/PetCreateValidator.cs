namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Pet;

public sealed class PetCreateValidator : AbstractValidator<PetCreateDto>
{
    public PetCreateValidator()
    {
        RuleFor(x => x.IdEspecie)
            .GreaterThan(0);

        RuleFor(x => x.IdRaca)
            .GreaterThan(0);

        RuleFor(x => x.NmPet)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DtNascimento)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("'DtNascimento' não pode ser uma data futura.");

        RuleFor(x => x.SgSexo)
            .Must(s => s == 'M' || s == 'F')
            .WithMessage("'SgSexo' deve ser 'M' ou 'F'.");

        RuleFor(x => x.SgPorte)
            .Must(p => p == 'P' || p == 'M' || p == 'G')
            .WithMessage("'SgPorte' deve ser 'P', 'M' ou 'G'.");

        // TUTOR_PET.DS_VINCULO é VARCHAR2(40) NOT NULL
        // (backend-tutor-java/.../V1__initial_schema.sql:75, só leitura). Sem NotEmpty():
        // PetCreateDto.DsVinculo já tem default "PROPRIETARIO" e o coalesce em PetService
        // satisfaz o NOT NULL do Oracle — mesmo padrão de DsFabricante em
        // VacinaCreateValidator (TASK-60). Achado 2 da revisão final do FIX_4: faltava a
        // metade "validação de tamanho" do padrão, deixando um dsVinculo > 40 chars
        // estourar ORA-12899 (500) em vez de 400.
        RuleFor(x => x.DsVinculo)
            .MaximumLength(40);
    }
}
