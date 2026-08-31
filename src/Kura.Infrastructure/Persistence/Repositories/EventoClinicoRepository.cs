namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class EventoClinicoRepository : Repository<EventoClinico>, IEventoClinicoRepository
{
    public EventoClinicoRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventoClinico>> GetByFiltersAsync(
        long? petId, long? tipoEventoId, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId)
    {
        var query = _dbSet.AsQueryable();

        if (petId.HasValue)
            query = query.Where(e => e.IdPet == petId.Value);

        if (tipoEventoId.HasValue)
            query = query.Where(e => e.IdTipoEvento == tipoEventoId.Value);

        if (dataInicio.HasValue)
            query = query.Where(e => e.DtEvento >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(e => e.DtEvento <= dataFim.Value);

        if (veterinarioId.HasValue)
            query = query.Where(e => e.IdVeterinario == veterinarioId.Value);

        return await query.OrderByDescending(e => e.DtEvento).ToListAsync();
    }

    /// <summary>
    /// FD-10 — ver <see cref="IEventoClinicoRepository.BuscarPorIdNaClinicaAsync"/>.
    ///
    /// <para><c>IgnoreQueryFilters()</c> + predicado de tenant escrito à mão, de propósito:
    /// o filtro de <c>EventoClinico</c> desliga inteiro quando não há clínica no contexto,
    /// então uma consulta que dependesse dele responderia diferente conforme o chamador
    /// tivesse ou não JWT. Escrito assim, trocar <c>e.IdClinica == idClinica</c> por
    /// <c>true</c> quebra <c>CobrancaServiceTests</c>.</para>
    /// </summary>
    public async Task<EventoClinico?> BuscarPorIdNaClinicaAsync(long id, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id && e.IdClinica == idClinica);
}
