namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class UsuarioClinicaRepository : Repository<UsuarioClinica>, IUsuarioClinicaRepository
{
    public UsuarioClinicaRepository(KuraDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>Por que <c>IgnoreQueryFilters()</c> aqui, e por que o predicado repete
    /// <c>StAtiva</c> à mão.</b> O filtro de tenant de <c>USUARIO_CLINICA</c>
    /// (<c>KuraDbContext.ApplyTenantFilters</c>) é
    /// <c>StAtiva &amp;&amp; (IdClinicaFiltro == null || IdClinica == IdClinicaFiltro)</c>:
    /// ele <b>DESLIGA INTEIRO</b> quando não há clínica no contexto — não nega. Depender
    /// dele aqui deixaria o resultado desta consulta dependente de estado ambiente que o
    /// login <b>não controla</b>.</para>
    ///
    /// <para>E esse estado ambiente existe de verdade: <c>POST /api/v1/auth/login</c> é
    /// <c>[AllowAnonymous]</c>, mas <c>UseAuthentication()</c> valida e popula
    /// <c>HttpContext.User</c> mesmo assim quando o cliente manda um <c>Authorization</c>
    /// válido. Um app que ainda tenha o token da sessão anterior no header (trocar de conta
    /// sem limpar o header é o caso trivial) faria <c>IdClinicaFiltro</c> ser NÃO nulo
    /// durante o próprio login, e a busca por e-mail seria silenciosamente escopada na
    /// clínica ERRADA — o usuário certo, com a senha certa, receberia "Email ou senha
    /// inválidos.". <c>IgnoreQueryFilters()</c> torna esta consulta determinística: mesmo
    /// resultado com ou sem JWT no header.</para>
    ///
    /// <para>A contrapartida é que o <c>StAtiva</c> que vinha embutido no filtro some junto,
    /// então ele é reescrito no <c>Where</c> — soft delete é regra do projeto (DELETE físico
    /// nunca acontece), e um usuário desativado não pode autenticar.</para>
    ///
    /// <para><c>OrderBy(IdClinica)</c> não escolhe nada — a decisão sobre N&gt;1 é do
    /// <c>AuthService</c>, que falha explicitamente. A ordenação existe só para a lista ser
    /// determinística em mensagens de diagnóstico e em teste.</para>
    /// </remarks>
    public async Task<IReadOnlyList<UsuarioClinica>> BuscarAtivosPorEmailAsync(string email) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(u => u.DsEmail == email && u.StAtiva)
            .OrderBy(u => u.IdClinica)
            .ToListAsync();
}
