namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IAgendamentoRepository
{
    Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(DateTime data, int limite);
}
