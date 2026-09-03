namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IAgendamentoRepository
{
    /// <summary>
    /// FD-17 — <c>idClinica</c> passou a ser obrigatório aqui e em
    /// <see cref="GetRecentesAsync"/>. <c>Agendamento</c> é a única entidade fora de
    /// <c>KuraDbContext.ApplyTenantFilters</c> (allowlist de compensação manual), e estes 2
    /// métodos consultavam <c>_context.Agendamentos</c> sem nenhum predicado de clínica —
    /// vazamento cross-tenant real na primeira tela pós-login (dashboard). Corrigido seguindo
    /// o mesmo padrão já usado em <see cref="GetByIdAsync"/> e em
    /// <c>AgendaService.cs</c>/<c>IAgendamentoReadRepository.GetByIntervaloAsync</c>: o
    /// consumidor (<c>DashboardService</c>) lê <c>IClinicaContext.IdClinica</c> e passa
    /// explicitamente — nunca confiar em filtro global aqui.
    /// </summary>
    Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(long idClinica, DateTime data, int limite);
    Task<IEnumerable<Agendamento>> GetRecentesAsync(long idClinica, DateTime referencia, int limite);
    Task<Agendamento?> GetByIdAsync(long id, long idClinica);

    /// <summary>
    /// FD-17 — conta agendamentos de teleconsulta cuja sessão foi iniciada no dia informado,
    /// escopados por clínica (mesma razão de <see cref="GetProximosDoDiaAsync"/>: <c>Agendamento</c>
    /// não tem filtro global). "Hoje" aqui é <c>DT_INICIO_SESSAO</c> (quando a sala foi
    /// efetivamente criada/usada), não <c>DT_AGENDAMENTO</c> — ver decisão registrada em
    /// <c>DashboardService.GetHojeAsync</c>.
    /// </summary>
    Task<int> ContarTeleorientacoesHojeAsync(long idClinica, DateTime data);

    void Update(Agendamento agendamento);
}
