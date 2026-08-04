namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITutorRepository : IRepository<Tutor>
{
    /// <summary>
    /// Busca tutores da clínica informada, opcionalmente filtrando por nome/CPF.
    /// TASK-21: idClinica é obrigatório — defesa em profundidade além do HasQueryFilter
    /// global do DbContext, que desliga inteiro (não nega) quando não há contexto de clínica.
    /// </summary>
    Task<IEnumerable<Tutor>> SearchAsync(string? busca, long idClinica);

    /// <summary>
    /// Busca um tutor por Id, restrito à clínica informada. Retorna null se o tutor
    /// não existir ou pertencer a outra clínica.
    /// </summary>
    Task<Tutor?> GetByIdAsync(long id, long idClinica);
}
