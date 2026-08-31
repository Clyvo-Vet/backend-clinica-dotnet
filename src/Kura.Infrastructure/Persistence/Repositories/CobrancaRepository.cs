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

    public async Task<Cobranca?> BuscarNoEventoDaClinicaAsync(
        long id, long idEventoClinico, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id
                                   && c.IdEventoClinico == idEventoClinico
                                   && c.IdClinica == idClinica
                                   && c.StAtiva);

    /// <summary>
    /// FD-11 — faixa semiaberta <c>[inicioInclusivo, fimExclusivo)</c>. Ver a interface para o
    /// porquê de o fim ser EXCLUSIVO e de esta consulta devolver linhas em vez de agregado.
    /// </summary>
    public async Task<IReadOnlyList<Cobranca>> ListarDaClinicaNoPeriodoAsync(
        long idClinica, DateTime inicioInclusivo, DateTime fimExclusivo) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(c => c.IdClinica == idClinica
                     && c.StAtiva
                     && c.DtCobranca >= inicioInclusivo
                     && c.DtCobranca < fimExclusivo)
            .OrderBy(c => c.DtCobranca)
            .ThenBy(c => c.Id)
            .ToListAsync();
}
