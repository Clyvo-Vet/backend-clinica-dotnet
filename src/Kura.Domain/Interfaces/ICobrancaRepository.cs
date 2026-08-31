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

    /// <summary>
    /// FD-11 — cobranças ATIVAS <b>desta clínica</b> cujo <c>DT_COBRANCA</c> cai no intervalo
    /// <b>semiaberto</b> <c>[inicioInclusivo, fimExclusivo)</c>.
    ///
    /// <para>
    /// 🔴 <b>O intervalo é semiaberto, e o nome dos parâmetros diz isso porque o modo de falha
    /// é silencioso.</b> Os KPI recebem duas datas <b>inclusivas</b> do gestor; um filtro
    /// escrito como <c>DtCobranca &lt;= ate</c> compara contra <c>ate 00:00:00</c> e descarta
    /// o último dia inteiro do relatório. Não há erro, não há log — só receita real que some.
    /// Quem chama converte a data final para <c>ate + 1 dia</c> e passa aqui como
    /// <b>exclusiva</b>.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Devolve LINHAS, não agregado — e isso é decisão de risco, não preguiça.</b> Este
    /// repositório não tem um único teste que toque Oracle: <c>GroupBy</c> com chave
    /// <b>nula</b> (<c>ID_SERVICO_PRECO</c> é nullable pela D-2) traduzido pelo provider
    /// Oracle é território não provado aqui, e o modo de falha desta casa é <i>verde no
    /// InMemory, 500 em produção</i>. A agregação acontece em memória, sobre esta única
    /// consulta de faixa, que é o que o índice <c>IDX_COBRANCA_CLINICA_DATA</c> da V18 serve.
    /// Volume mensal de uma clínica é da ordem de centenas de linhas.
    /// </para>
    ///
    /// <para>
    /// <b>Uma consulta cobre os DOIS períodos</b> (o pedido e o de comparação): quem chama
    /// passa a faixa combinada e reparte em memória. Um round-trip, um seek de índice.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Cobranca>> ListarDaClinicaNoPeriodoAsync(
        long idClinica, DateTime inicioInclusivo, DateTime fimExclusivo);
}
