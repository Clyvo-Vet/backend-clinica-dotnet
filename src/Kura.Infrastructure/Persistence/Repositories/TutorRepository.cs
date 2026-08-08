namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class TutorRepository : Repository<Tutor>, ITutorRepository
{
    public TutorRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Tutor>> SearchAsync(string? busca, long idClinica)
    {
        var query = _dbSet.Where(t => t.IdClinica == idClinica);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var lower = busca.ToLower();
            query = query.Where(t => t.NmTutor.ToLower().Contains(lower) || t.NrCpf.Contains(busca));
        }

        return await query.ToListAsync();
    }

    public Task<Tutor?> GetByIdAsync(long id, long idClinica)
        => _dbSet.FirstOrDefaultAsync(t => t.Id == id && t.IdClinica == idClinica);

    public Task<Tutor?> GetByTelefoneAsync(string numero)
        => _dbSet.FirstOrDefaultAsync(t => t.NrTelefone == numero);
}
