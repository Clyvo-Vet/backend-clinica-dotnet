namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;

public interface ITutorService
{
    Task<IEnumerable<TutorResponseDto>> SearchAsync(string? busca);
    Task<TutorResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<PetResponseDto>> GetPetsAsync(long id);
    Task<TutorComInviteResponseDto> CreateAsync(TutorCreateDto dto, long clinicaId);
    Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto);
    Task SoftDeleteAsync(long id);
}
