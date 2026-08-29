namespace Kura.Domain.Interfaces;

public interface IClinicaContext
{
    long IdClinica { get; }

    /// <summary>
    /// <c>ID_VETERINARIO</c> do usuário logado, ou <c>null</c> quando ele não é veterinário.
    ///
    /// <para>🔴 <b>Passou a ser nullable na FD-03, e isso é uma mudança de contrato
    /// deliberada.</b> Antes era <c>long</c> resolvido por <c>GetRequiredClaimValue</c>, que
    /// <b>LANÇA</b> <see cref="UnauthorizedAccessException"/> se a claim faltar. Com o login
    /// por <c>USUARIO_CLINICA</c>, um GESTOR que não é veterinário passa a existir e seu
    /// token <b>não carrega</b> a claim <c>veterinarioId</c> — manter o membro lançando
    /// transformaria "usuário sem vínculo clínico" em erro de autorização em qualquer ponto
    /// que apenas consultasse o contexto.</para>
    ///
    /// <para><b>Por que é seguro trocar o tipo:</b> medido nesta task por varredura em
    /// <c>src/</c> e <c>tests/</c> — <b>nenhum</b> consumidor lê este membro (todos os outros
    /// hits de <c>IdVeterinario</c> são campo de DTO, propriedade de entidade, configuração
    /// EF ou parâmetro de método vindo do corpo/query). <b>Controle positivo:</b> a mesma
    /// forma de busca sobre <c>Context.IdClinica</c> devolve 10+ consumidores reais, então o
    /// zero é ausência de consumidor, não busca cega.</para>
    /// </summary>
    long? IdVeterinario { get; }

    long? IdClinicaFiltro { get; }

    /// <summary>
    /// Papel do usuário logado (<c>GESTOR</c> / <c>VETERINARIO</c> — ver
    /// <c>PerfisUsuarioClinica</c>), lido da claim <c>perfil</c> emitida pela FD-03.
    /// <c>null</c> quando não há token, ou quando o token é anterior à FD-03 (tokens
    /// emitidos antes desta mudança não têm a claim e continuam válidos até expirar).
    /// </summary>
    string? Perfil { get; }
}
