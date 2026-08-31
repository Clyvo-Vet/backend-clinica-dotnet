namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// FD-10 — implementação de <see cref="ICobrancaRepository"/>. Ver a interface para o porquê
/// de <c>IgnoreQueryFilters()</c> + predicado de tenant escrito à mão.
/// </summary>
public class CobrancaRepository : Repository<Cobranca>, ICobrancaRepository
{
    public CobrancaRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Cobranca>> ListarDoEventoNaClinicaAsync(
        long idEventoClinico, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(c => c.IdEventoClinico == idEventoClinico
                     && c.IdClinica == idClinica
                     && c.StAtiva)
            .OrderByDescending(c => c.DtCobranca)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

    public async Task<Cobranca?> BuscarPorIdNaClinicaAsync(long id, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.IdClinica == idClinica && c.StAtiva);
}
