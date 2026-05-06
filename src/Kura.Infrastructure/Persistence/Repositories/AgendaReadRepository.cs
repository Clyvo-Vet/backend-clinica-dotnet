namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class AgendaReadRepository(KuraDbContext context) : IAgendamentoReadRepository
{
    public async Task<IEnumerable<Agendamento>> GetByIntervaloAsync(
        long idClinica, DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        var query = context.Agendamentos
            .Include(a => a.Pet)
            .Include(a => a.Tutor)
            .Include(a => a.Veterinario)
            .Where(a => a.IdClinica == idClinica
                     && a.DtAgendamento >= dataInicio
                     && a.DtAgendamento <= dataFim);

        if (idVeterinario.HasValue)
            query = query.Where(a => a.IdVeterinario == idVeterinario.Value);

        return await query.OrderBy(a => a.DtAgendamento).ToListAsync();
    }
}
