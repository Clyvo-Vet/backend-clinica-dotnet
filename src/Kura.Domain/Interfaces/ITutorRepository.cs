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

    /// <summary>
    /// TASK-67: busca por telefone, deliberadamente SEM escopo de clínica — consumida
    /// pela IA Luna (GET /api/v1/tutores/telefone/{numero}), que não tem JWT de clínica
    /// e é justamente quem precisa descobrir a clínica a partir do telefone. Diferente
    /// de SearchAsync/GetByIdAsync(id, idClinica) acima, que assumem contexto de clínica
    /// já resolvido. O EF ainda aplica o HasQueryFilter de StAtiva (soft delete) — a
    /// parte de tenant do filtro fica inerte porque a chamada não tem
    /// IClinicaContext.IdClinicaFiltro (sem JWT), não porque foi ignorada de propósito.
    /// </summary>
    Task<Tutor?> GetByTelefoneAsync(string numero);
}
