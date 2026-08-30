namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// FD-09 — implementação de <see cref="IServicoPrecoRepository"/>. Ver a interface para o
/// porquê de <c>IgnoreQueryFilters()</c> + predicado de tenant escrito à mão.
/// </summary>
public class ServicoPrecoRepository : Repository<ServicoPreco>, IServicoPrecoRepository
{
    public ServicoPrecoRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ServicoPreco>> ListarDaClinicaAsync(long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(s => s.IdClinica == idClinica && s.StAtiva)
            .OrderBy(s => s.NmServico)
            .ToListAsync();

    public async Task<ServicoPreco?> BuscarPorIdNaClinicaAsync(long id, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.IdClinica == idClinica);

    public async Task<ServicoPreco?> BuscarAtivoPorNomeNaClinicaAsync(
        long idClinica, string nmServico, long? excetoId = null)
    {
        // ToUpper() e não ToUpperInvariant(): o provider Oracle traduz o primeiro para
        // UPPER(...) e não conhece o segundo (que viraria avaliação client-side, trazendo a
        // tabela inteira). O provider InMemory executa o método CLR de verdade, então a
        // comparação tem o mesmo significado nos dois — o que mantém o teste honesto.
        var alvo = nmServico.Trim().ToUpper();

        return await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.IdClinica == idClinica
                                   && s.StAtiva
                                   && s.NmServico.ToUpper() == alvo
                                   && (excetoId == null || s.Id != excetoId));
    }
}
