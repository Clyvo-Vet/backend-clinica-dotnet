namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class TutorRepository : Repository<Tutor>, ITutorRepository
{
    public TutorRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Tutor>> SearchAsync(string busca)
    {
        var lower = busca.ToLower();
        return await _dbSet
            .Where(t => t.NmTutor.ToLower().Contains(lower) || t.NrCpf.Contains(busca))
            .ToListAsync();
    }
}
