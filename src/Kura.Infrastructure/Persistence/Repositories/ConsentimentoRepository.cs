namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ConsentimentoRepository(KuraDbContext context) : IConsentimentoRepository
{
    public Task<Consentimento?> GetMaisRecenteAsync(long idTutor, string dsTipo)
        => context.Consentimentos
            .Where(c => c.IdTutor == idTutor && c.DsTipo == dsTipo)
            .OrderByDescending(c => c.DtConsentimento)
            .FirstOrDefaultAsync();
}
