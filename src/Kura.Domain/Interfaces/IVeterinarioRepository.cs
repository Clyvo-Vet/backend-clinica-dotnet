namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IVeterinarioRepository : IRepository<Veterinario>
{
    Task<IEnumerable<Veterinario>> GetAllByClinicaIdAsync(long idClinica);

    /// <summary>
    /// FD-04 — busca por PK <b>ignorando os query filters</b>, para validar explicitamente a
    /// clinica do veterinario antes de vincula-lo a um <c>USUARIO_CLINICA</c>.
    ///
    /// <para>🔴 <b>Por que nao usar <c>GetByIdAsync</c>:</b> com JWT no contexto, o query
    /// filter de <c>Veterinario</c> ja devolveria <c>null</c> para vet de outro tenant — e o
    /// isolamento passaria a depender de estado AMBIENTE, nao de codigo. Consequencia
    /// pratica: a comparacao explicita no service viraria inalcancavel, ou seja, um teste de
    /// mutacao sobre ela continuaria VERDE, e a garantia deixaria de ser verificavel. Com
    /// esta busca cega de tenant, quem nega o cruzamento e uma linha de codigo que um teste
    /// consegue quebrar.</para>
    ///
    /// <para>A defesa importa porque <c>FK_USUARIO_CLINICA_VET</c> (V17) referencia so
    /// <c>VETERINARIO(ID_VETERINARIO)</c>, <b>sem compor com <c>ID_CLINICA</c></b>: o Oracle
    /// aceita o vinculo cruzado. Mesmo achado da revisao G2 da FD-03.</para>
    ///
    /// <para>⚠️ Quem chamar este metodo assume a responsabilidade do escopo: ele NAO filtra
    /// clinica nem <c>ST_ATIVA</c>.</para>
    /// </summary>
    Task<Veterinario?> BuscarPorIdIgnorandoFiltrosAsync(long id);
}
