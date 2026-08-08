namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITriagemLunaRepository
{
    Task<List<TriagemLuna>> GetByIntervaloAsync(DateTime dataInicio, DateTime dataFim);

    /// <summary>
    /// TASK-67: primeira escrita nesta tabela — POST /api/v1/luna/triage.
    /// </summary>
    Task AddAsync(TriagemLuna entidade);
}
