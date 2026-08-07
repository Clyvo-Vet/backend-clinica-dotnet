namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Interfaces;
using Kura.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

public class TimelineRepository : ITimelineRepository
{
    private readonly KuraDbContext _context;

    public TimelineRepository(KuraDbContext context) => _context = context;

    public async Task<IEnumerable<TimelineEntry>> GetByPetIdAsync(long idPet)
    {
        // TASK-63: antes consultava VW_TIMELINE_PET via FromSqlRaw. Essa view (Flyway
        // V1/V6, backend-tutor-java) é derivada de AGENDAMENTO e documentada desde a V1
        // como "lida pelo Java (TimelineService)" — não tem DS_OBSERVACAO nem
        // NM_VETERINARIO, campos que o read model .NET espera, causando ORA-00904 contra
        // o Oracle real. EventoClinico já tem tudo que TimelineEntry precisa e já está
        // em KuraDbContext.ApplyTenantFilters, então consultá-lo via LINQ herda o
        // isolamento de tenant automaticamente (sem depender de filtro manual aqui).
        var eventos = await _context.EventosClinicos
            .Include(e => e.Pet)
            .Include(e => e.Veterinario)
            .Include(e => e.TipoEvento)
            .Where(e => e.IdPet == idPet)
            .OrderByDescending(e => e.DtEvento)
            .ToListAsync();

        return eventos.Select(e => new TimelineEntry(
            e.Id,
            e.IdPet,
            e.Pet.NmPet,
            e.TipoEvento.NmTipo,
            e.DtEvento,
            e.DsObservacao,
            e.Veterinario.NmVeterinario));
    }
}
