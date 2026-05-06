namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class TriagemLunaRepository(KuraDbContext context) : ITriagemLunaRepository
{
    public async Task<List<TriagemLuna>> GetByIntervaloAsync(DateTime dataInicio, DateTime dataFim)
    {
        return await context.TriagensLuna
            .Where(t => t.DtTriagem >= dataInicio && t.DtTriagem <= dataFim)
            .ToListAsync();
    }
}
