namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.CrossCutting.Observability;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class AgendaReadRepository(KuraDbContext context) : IAgendamentoReadRepository
{
    public async Task<IEnumerable<Agendamento>> GetByIntervaloAsync(
        long idClinica, DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        // S3D-04b: span-filho de camada Infrastructure — filho do span de
        // Application acima (AgendaService), que por sua vez é filho do span HTTP.
        // Prova a hierarquia de 3 níveis: API -> Application -> Infrastructure -> Oracle.
        using var activity = KuraActivitySource.Instancia.StartActivity("Infrastructure.AgendaReadRepository.GetByIntervaloAsync");
        activity?.SetTag("kura.layer", "Infrastructure");
        activity?.SetTag("kura.id_clinica", idClinica);

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
