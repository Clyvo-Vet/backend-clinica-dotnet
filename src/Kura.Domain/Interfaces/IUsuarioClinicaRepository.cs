namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

/// <summary>
/// Acesso a <see cref="UsuarioClinica"/> (FD-03, ciclo FIN; ampliado pela FD-04).
///
/// <para>
/// 🔴 <b>Todo método desta interface escreve o predicado de tenant À MÃO e ignora os query
/// filters.</b> O filtro de <c>USUARIO_CLINICA</c> em <c>KuraDbContext.ApplyTenantFilters</c>
/// <b>DESLIGA INTEIRO</b> quando <c>IdClinicaFiltro</c> é null (não nega), então depender
/// dele aqui faria o resultado destas consultas variar conforme estado ambiente que o
/// chamador não controla. Com a clínica recebida por parâmetro, o mesmo argumento produz o
/// mesmo resultado com ou sem JWT no header — e o isolamento vira algo que um teste pode
/// mutar e ver quebrar, em vez de um efeito colateral de configuração.
/// </para>
/// </summary>
public interface IUsuarioClinicaRepository : IRepository<UsuarioClinica>
{
    /// <summary>
    /// Todos os usuários ATIVOS com este e-mail, em TODAS as clínicas.
    ///
    /// <para>⚠️ <b>Devolve coleção, e não um único usuário, de propósito.</b> A UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c> — e-mail é único POR CLÍNICA, não globalmente —, então
    /// "o usuário deste e-mail" não é uma pergunta com resposta única. Quem chama tem que
    /// decidir explicitamente o que fazer quando vier mais de um; devolver
    /// <c>UsuarioClinica?</c> aqui esconderia essa decisão dentro de um
    /// <c>FirstOrDefault()</c>, que é exatamente a forma de escolha arbitrária e silenciosa
    /// de tenant que a FD-03 existe para eliminar.</para>
    ///
    /// <para><b>A busca ignora os query filters e escreve o predicado inteiro à mão</b> —
    /// ver a implementação para o argumento medido.</para>
    /// </summary>
    Task<IReadOnlyList<UsuarioClinica>> BuscarAtivosPorEmailAsync(string email);

    /// <summary>
    /// FD-04 — usuários ATIVOS de uma clínica, ordenados por e-mail (ordem estável para
    /// listagem e para asserção de teste).
    /// </summary>
    Task<IReadOnlyList<UsuarioClinica>> ListarDaClinicaAsync(long idClinica);

    /// <summary>
    /// FD-04 — um usuário pelo par (id, clínica), <b>incluindo desativados</b>.
    ///
    /// <para>A clínica entra no predicado, e não como conferência posterior: um id de outro
    /// tenant devolve <c>null</c>, que o service traduz em <c>404</c> — a resposta é
    /// indistinguível de "não existe", então o endpoint não vira oráculo de existência de id
    /// alheio.</para>
    ///
    /// <para>Inclui desativados porque um GESTOR administrando usuários precisa enxergar
    /// quem ele desativou; o estado vai no <c>ST_ATIVA</c> da resposta.</para>
    /// </summary>
    Task<UsuarioClinica?> BuscarPorIdNaClinicaAsync(long id, long idClinica);

    /// <summary>
    /// FD-04 — usuário pelo par (clínica, e-mail), <b>incluindo desativados</b>. É a checagem
    /// explícita de <c>UK_USUARIO_CLINICA_EMAIL</c>.
    ///
    /// <para>🔴 <b>Incluir desativados não é zelo, é correção.</b> Soft delete é a regra do
    /// projeto: <c>ST_ATIVA='N'</c> mantém a LINHA no Oracle, e a linha continua ocupando a
    /// unique key. Uma checagem que só olhasse ativos aprovaria o e-mail e o
    /// <c>INSERT</c> morreria com <c>ORA-00001</c> — <c>500</c> de constraint, que é
    /// exatamente o que esta task tem de evitar. O provider InMemory da suíte <b>não valida
    /// índice único</b>, então nenhum teste desta suíte pegaria o erro no INSERT: a única
    /// defesa é este predicado.</para>
    /// </summary>
    /// <param name="excetoId">
    /// Quando informado, ignora essa linha na busca. Existe para a REATIVAÇÃO (fix wave
    /// pós-G2 da FD-04): ao reativar, o próprio usuário desativado é o dono do e-mail, e um
    /// <c>FirstOrDefault</c> poderia devolvê-lo — fazendo o usuário colidir CONSIGO MESMO e
    /// tornando a reativação impossível. A pergunta que a reativação precisa fazer é "existe
    /// OUTRO com este e-mail", e ela tem de estar escrita no predicado, não resolvida depois
    /// por comparação de id sobre um resultado de ordem indefinida.
    /// </param>
    Task<UsuarioClinica?> BuscarPorEmailNaClinicaAsync(
        long idClinica, string email, long? excetoId = null);

    /// <summary>
    /// FD-04 — quantos <c>GESTOR</c> ATIVOS a clínica tem, <b>excluindo</b> opcionalmente um
    /// id (o usuário que a operação em curso está prestes a rebaixar ou desativar).
    ///
    /// <para>É o instrumento do invariante "nenhuma operação pode deixar a clínica com zero
    /// GESTOR ativo" — ver <c>UsuarioClinicaService</c> para o argumento da decisão.</para>
    /// </summary>
    Task<int> ContarGestoresAtivosAsync(long idClinica, long? excetoId = null);
}
