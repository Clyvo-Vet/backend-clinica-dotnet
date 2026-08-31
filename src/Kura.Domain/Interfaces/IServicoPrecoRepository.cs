namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

/// <summary>
/// FD-09 — acesso a <see cref="ServicoPreco"/> com a clínica como <b>argumento explícito</b>.
///
/// <para>
/// 🔴 <b>Nenhuma das consultas abaixo depende do query filter de tenant.</b> O filtro de
/// <c>ServicoPreco</c> (<c>KuraDbContext.ApplyTenantFilters</c>) desliga INTEIRO quando
/// <c>IdClinicaFiltro</c> é null — ele nega nada, não nega tudo. Uma consulta que dependesse
/// dele daria resultado diferente conforme houvesse ou não JWT no contexto, e é exatamente
/// aqui que essa variação vira vazamento cross-tenant ou <c>404</c> falso. Mesma disciplina
/// de <see cref="IUsuarioClinicaRepository"/>, e pelo mesmo motivo.
/// </para>
///
/// <para>
/// Efeito colateral desejado: o isolamento vira <b>mutável por teste</b>. Trocar
/// <c>s.IdClinica == idClinica</c> por <c>true</c> na implementação quebra
/// <c>ServicoPrecoServiceTests</c>; um isolamento que existisse só via query filter
/// continuaria verde com o filtro removido sempre que o contexto estivesse vazio.
/// </para>
/// </summary>
public interface IServicoPrecoRepository : IRepository<ServicoPreco>
{
    /// <summary>Itens ATIVOS da tabela de preços de uma clínica, ordenados por nome.</summary>
    Task<IReadOnlyList<ServicoPreco>> ListarDaClinicaAsync(long idClinica);

    /// <summary>
    /// Busca por id <b>dentro da clínica informada</b>, ativo ou não. Devolver o item
    /// desativado é requisito: sem isso a reativação não teria como encontrá-lo, e desativar
    /// viraria porta de mão única (A-3 da FD-04).
    /// </summary>
    Task<ServicoPreco?> BuscarPorIdNaClinicaAsync(long id, long idClinica);

    /// <summary>
    /// Procura um item <b>ATIVO</b> da clínica com este nome (comparação sem distinção de
    /// caixa), opcionalmente ignorando um id.
    ///
    /// <para>
    /// 🔴 <b>"ATIVO" no nome não é detalhe, é a regra de produto da task.</b> A FD-07
    /// deliberadamente NÃO criou <c>UNIQUE (ID_CLINICA, NM_SERVICO)</c> justamente para que um
    /// serviço desativado possa ser recadastrado. Um método que buscasse também os inativos
    /// reintroduziria, no código, a unicidade que o schema evitou de propósito — e com ela o
    /// defeito A-3 da FD-04 (o e-mail queimado para sempre).
    /// </para>
    /// </summary>
    Task<ServicoPreco?> BuscarAtivoPorNomeNaClinicaAsync(
        long idClinica, string nmServico, long? excetoId = null);

    /// <summary>
    /// FD-11 — RÓTULOS dos serviços cujos ids aparecem no mix, <b>ativos ou não</b>.
    ///
    /// <para>
    /// 🔴 <b>Este método existe para NÃO filtrar por <c>StAtiva</c>, e essa é a regra inteira
    /// dele.</b> O mix por serviço agrupa cobranças já lançadas; o nome do serviço entra como
    /// <b>rótulo</b>, nunca como valor (o valor é a cópia em <c>VL_COBRADO</c>). Um serviço
    /// desativado <b>depois</b> de faturar continua tendo faturado: filtrar os inativos aqui
    /// apagaria a receita dele do relatório <b>em silêncio</b> — sem erro, sem log, só um mix
    /// que deixa de somar o total. É o mesmo raciocínio de
    /// <see cref="BuscarPorIdNaClinicaAsync"/>, que também devolve o desativado de propósito,
    /// e o oposto deliberado de <see cref="ListarDaClinicaAsync"/>, que é o catálogo
    /// oferecível hoje.
    /// </para>
    ///
    /// <para>
    /// O predicado de clínica continua escrito à mão: um rótulo trazido da clínica errada
    /// seria vazamento de nome de serviço do concorrente dentro de um relatório.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ServicoPreco>> ListarPorIdsNaClinicaAsync(
        IReadOnlyCollection<long> ids, long idClinica);
}
