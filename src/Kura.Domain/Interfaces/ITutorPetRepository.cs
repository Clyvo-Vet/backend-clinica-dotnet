namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITutorPetRepository
{
    Task AddAsync(TutorPet tutorPet);
    Task<bool> ExistsAsync(long idTutor, long idPet);
    Task<IEnumerable<TutorPet>> GetByPetIdAsync(long idPet);
    Task<IEnumerable<TutorPet>> GetByTutorIdAsync(long idTutor);
}
