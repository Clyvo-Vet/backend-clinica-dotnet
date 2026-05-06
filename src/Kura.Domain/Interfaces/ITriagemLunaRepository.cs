namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITriagemLunaRepository
{
    Task<List<TriagemLuna>> GetByIntervaloAsync(DateTime dataInicio, DateTime dataFim);
}
