namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IEventoClinicoRepository : IRepository<EventoClinico>
{
    Task<IEnumerable<EventoClinico>> GetByFiltersAsync(
        long? petId, long? tipoEventoId, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId);

    /// <summary>
    /// FD-10 — busca um evento clínico por id <b>dentro da clínica informada</b>, com o
    /// predicado de tenant escrito à mão e os query filters ignorados.
    ///
    /// <para>
    /// 🔴 <b>Existe porque <c>GetByIdAsync</c> NÃO serve para esta pergunta.</b> Ele resolve
    /// por <c>FindAsync</c>, que consulta a PK direto (e pode até devolver a instância já
    /// rastreada), <b>sem passar por query filter nenhum</b> — o evento de outra clínica
    /// volta normalmente. Um lançamento de cobrança escrito sobre ele penduraria receita da
    /// clínica A num atendimento da clínica B, e a FK do Oracle não impediria: a
    /// <c>FK_COBRANCA_EVENTO</c> da V18 referencia só <c>EVENTO_CLINICO(ID_EVENTO)</c>,
    /// <b>sem compor com <c>ID_CLINICA</c></b>. É a mesma forma da armadilha F1 da FD-03
    /// (<c>FK_USUARIO_CLINICA_VET</c> sem a clínica), onde a ausência da comparação era a
    /// única coisa entre o schema e o vazamento.
    /// </para>
    /// </summary>
    Task<EventoClinico?> BuscarPorIdNaClinicaAsync(long id, long idClinica);
}
