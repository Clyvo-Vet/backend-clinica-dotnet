namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class TutorPetRepository : ITutorPetRepository
{
    private readonly KuraDbContext _context;

    public TutorPetRepository(KuraDbContext context) => _context = context;

    public async Task AddAsync(TutorPet tutorPet)
    {
        await _context.TutorPets.AddAsync(tutorPet);
    }

    public async Task<bool> ExistsAsync(long idTutor, long idPet)
    {
        return await _context.TutorPets
            .AnyAsync(tp => tp.IdTutor == idTutor && tp.IdPet == idPet);
    }

    public async Task<IEnumerable<TutorPet>> GetByPetIdAsync(long idPet)
    {
        return await _context.TutorPets
            .Include(tp => tp.Tutor)
            .Where(tp => tp.IdPet == idPet)
            .ToListAsync();
    }

    public async Task<IEnumerable<TutorPet>> GetByTutorIdAsync(long idTutor)
    {
        return await _context.TutorPets
            .Include(tp => tp.Pet)
            .Where(tp => tp.IdTutor == idTutor)
            .ToListAsync();
    }
}
