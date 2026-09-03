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

    /// <summary>
    /// FD-16 — o predicado <c>(incluirInativos || s.StAtiva)</c> vai DENTRO do
    /// <c>Where</c> traduzido para SQL, não filtrado depois em memória — numa clínica
    /// grande, resolver isto pós-<c>ToListAsync()</c> traria a tabela inteira para o
    /// processo. Ver o comentário de <see cref="ListarPorIdsNaClinicaAsync"/> sobre por que
    /// avaliação client-side acidental já mordeu este repositório antes.
    /// </summary>
    public async Task<IReadOnlyList<ServicoPreco>> ListarDaClinicaAsync(
        long idClinica, bool incluirInativos = false) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(s => s.IdClinica == idClinica && (incluirInativos || s.StAtiva))
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

    /// <summary>
    /// FD-11 — rótulos do mix. 🔴 <b>Sem <c>StAtiva</c> no predicado, de propósito</b> — ver a
    /// interface. Acrescentar <c>&amp;&amp; s.StAtiva</c> aqui apaga do relatório a receita de
    /// um serviço desativado depois de faturar.
    /// </summary>
    public async Task<IReadOnlyList<ServicoPreco>> ListarPorIdsNaClinicaAsync(
        IReadOnlyCollection<long> ids, long idClinica)
    {
        if (ids.Count == 0)
            return [];

        // Array, e nao o IReadOnlyCollection recebido: a traducao de Contains para IN (...)
        // e garantida para array/List no provider relacional; um tipo de colecao que o
        // provider nao reconheca degrada para avaliacao client-side, que traria a tabela de
        // precos inteira da clinica para a memoria -- e o InMemory da suite nao mostraria
        // diferenca nenhuma.
        var alvos = ids.ToArray();

        return await _dbSet
            .IgnoreQueryFilters()
            .Where(s => s.IdClinica == idClinica && alvos.Contains(s.Id))
            .ToListAsync();
    }
}
