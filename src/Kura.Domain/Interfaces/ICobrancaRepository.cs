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

    /// <summary>Busca uma cobrança por id <b>dentro da clínica informada</b>.</summary>
    Task<Cobranca?> BuscarPorIdNaClinicaAsync(long id, long idClinica);
}
