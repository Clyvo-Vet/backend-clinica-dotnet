namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class AgendamentoRepository : IAgendamentoRepository
{
    private readonly KuraDbContext _context;

    public AgendamentoRepository(KuraDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(DateTime data, int limite)
    {
        return await _context.Agendamentos
            .Where(a => a.DtAgendamento.Date == data.Date && a.DtAgendamento >= DateTime.UtcNow)
            .OrderBy(a => a.DtAgendamento)
            .Take(limite)
            .ToListAsync();
    }
}
