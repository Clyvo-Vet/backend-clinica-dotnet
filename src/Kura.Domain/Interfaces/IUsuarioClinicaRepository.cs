namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

/// <summary>
/// Acesso a <see cref="UsuarioClinica"/> (FD-03, ciclo FIN).
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
}
