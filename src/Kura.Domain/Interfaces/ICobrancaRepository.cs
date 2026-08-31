namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

/// <summary>
/// FD-10 — acesso a <see cref="Cobranca"/> com a clínica como <b>argumento explícito</b>.
///
/// <para>
/// 🔴 <b>Nenhuma consulta aqui depende do query filter de tenant.</b> O filtro de
/// <c>Cobranca</c> (<c>KuraDbContext.ApplyTenantFilters</c>) <b>desliga inteiro</b> quando
/// <c>IdClinicaFiltro</c> é null — ele nega nada, não nega tudo. Uma consulta que dependesse
/// dele daria resultado diferente conforme houvesse ou não JWT no contexto, e é aí que a
/// variação vira vazamento cross-tenant. Mesma disciplina de
/// <see cref="IServicoPrecoRepository"/>, e pelo mesmo motivo.
/// </para>
///
/// <para>
/// Efeito colateral desejado: o isolamento vira <b>mutável por teste</b>. Trocar
/// <c>c.IdClinica == idClinica</c> por <c>true</c> na implementação quebra
/// <c>CobrancaServiceTests</c>; um isolamento que existisse só via query filter continuaria
/// verde com o filtro removido sempre que o contexto estivesse vazio.
/// </para>
/// </summary>
public interface ICobrancaRepository : IRepository<Cobranca>
{
    /// <summary>
    /// Cobranças ATIVAS lançadas num evento clínico <b>desta clínica</b>, mais recentes
    /// primeiro.
    /// </summary>
    Task<IReadOnlyList<Cobranca>> ListarDoEventoNaClinicaAsync(long idEventoClinico, long idClinica);

    /// <summary>
    /// Busca uma cobrança por id <b>dentro da clínica informada E pendurada no evento
    /// informado</b>.
    ///
    /// <para>
    /// 🔴 <b>O <c>idEventoClinico</c> entra no predicado por causa do achado F1 da revisão
    /// G2 da FD-10.</b> A versão anterior filtrava só por id + clínica, e a rota é
    /// <c>/eventos-clinicos/{idEventoClinico}/cobrancas/{id}</c>: o segmento do meio era
    /// aceito com <b>qualquer</b> valor — evento de outro tenant e evento inexistente
    /// (<c>999999</c>) devolviam <c>200</c>, medido. Não era vazamento cross-tenant (a
    /// cobrança em si já era filtrada por clínica), mas o XML doc do método <b>prometia um
    /// <c>404</c> que nunca acontecia</b> — "documentação que garante o que o código não
    /// faz", a classe de defeito mais repetida deste projeto — e divergia do <c>Listar</c>
    /// irmão, que valida. Duas regras diferentes para a mesma rota-pai, no mesmo controller,
    /// é armadilha para quem vier depois.
    /// </para>
    /// </summary>
    Task<Cobranca?> BuscarNoEventoDaClinicaAsync(long id, long idEventoClinico, long idClinica);
}
