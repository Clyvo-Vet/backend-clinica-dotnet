namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IAgendamentoReadRepository
{
    Task<IEnumerable<Agendamento>> GetByIntervaloAsync(
        long idClinica, DateTime dataInicio, DateTime dataFim, long? idVeterinario);
}
