namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IAgendamentoRepository
{
    Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(DateTime data, int limite);
    Task<Agendamento?> GetByIdAsync(long id, long idClinica);
    void Update(Agendamento agendamento);
}
