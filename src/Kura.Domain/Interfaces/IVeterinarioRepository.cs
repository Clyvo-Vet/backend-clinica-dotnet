namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IVeterinarioRepository : IRepository<Veterinario>
{
    Task<IEnumerable<Veterinario>> GetAllByClinicaIdAsync(long idClinica);

    /// <summary>
    /// FD-04 — busca por PK <b>ignorando os query filters</b>, para validar explicitamente a
    /// clinica do veterinario antes de vincula-lo a um <c>USUARIO_CLINICA</c>.
    ///
    /// <para>🔴 <b>Por que nao usar <c>GetByIdAsync</c> — MEDIDO na fix wave pos-G2, e a
    /// primeira versao deste paragrafo estava errada.</b> Ela alegava que a comparacao no
    /// service ficaria inalcancavel e que "um teste de mutacao sobre ela continuaria VERDE".
    /// Falso: os testes de service rodam com os filtros DESLIGADOS, entao 2 deles mordem de
    /// qualquer forma. O que <b>de fato</b> se perde com <c>GetByIdAsync</c> e a mordida sobre
    /// o <b>caminho de producao</b>: medido, o assembly HTTP inteiro fica VERDE (45/45) com a
    /// comparacao apagada, porque o query filter transforma o veterinario alheio em
    /// <c>null</c> e o vazamento deixa de ser observavel por HTTP. O isolamento passaria a
    /// depender de estado AMBIENTE — e o filtro desliga inteiro quando nao ha clinica no
    /// contexto. Ver <c>UsuarioClinicaService.GarantirVeterinarioDaClinicaAsync</c> para as
    /// duas medicoes.</para>
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
