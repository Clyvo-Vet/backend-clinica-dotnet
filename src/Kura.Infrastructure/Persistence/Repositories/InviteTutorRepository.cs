namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class InviteTutorRepository : Repository<InviteTutor>, IInviteTutorRepository
{
    public InviteTutorRepository(KuraDbContext context) : base(context) { }

    public Task<InviteTutor?> GetByTokenAsync(Guid token)
        => _dbSet.FirstOrDefaultAsync(i => i.NrToken == token);

    public Task<bool> ExisteInviteAtivoAsync(long idTutor)
        => _dbSet.AnyAsync(i => i.IdTutor == idTutor && i.StUtilizado == 'N');
}
