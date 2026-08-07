namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Pet;

public sealed class AdicionarTutorPetValidator : AbstractValidator<AdicionarTutorPetDto>
{
    public AdicionarTutorPetValidator()
    {
        // TUTOR_PET.DS_VINCULO é VARCHAR2(40) NOT NULL
        // (backend-tutor-java/.../V1__initial_schema.sql:75, só leitura). Sem NotEmpty():
        // AdicionarTutorPetDto.DsVinculo já tem default "CUIDADOR" e o coalesce em
        // PetService.AdicionarTutorAsync satisfaz o NOT NULL do Oracle — mesmo padrão de
        // DsFabricante em VacinaCreateValidator (TASK-60). Achado 2 da revisão final do
        // FIX_4: AdicionarTutorPetDto não tinha validator nenhum, então um dsVinculo > 40
        // chars estourava ORA-12899 (500) em vez de 400.
        RuleFor(x => x.DsVinculo)
            .MaximumLength(40);
    }
}
