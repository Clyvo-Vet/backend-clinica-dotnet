namespace Kura.Api.Extensions;

using Kura.Api.Services;
using Kura.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Nomes das políticas de autorização da API. Constantes, e não string solta em cada
/// <c>[Authorize(Policy = "...")]</c>: um literal com erro de digitação NÃO degrada em
/// silêncio no ASP.NET Core (a requisição morre com
/// <c>InvalidOperationException: The AuthorizationPolicy named: 'X' was not found</c>),
/// mas a constante torna a ligação verificável pelo compilador em vez de pelo runtime.
/// </summary>
public static class PoliticasAutorizacao
{
    /// <summary>
    /// Exige a claim <c>perfil</c> com o valor <c>GESTOR</c>
    /// (<see cref="PerfisUsuarioClinica.Gestor"/>), emitida por
    /// <c>AuthService.GenerateToken</c> desde a FD-03.
    /// </summary>
    public const string SomenteGestor = "SomenteGestor";
}

/// <summary>
/// FD-04 (ciclo FIN) — <b>a primeira política de autorização deste backend</b>. Até esta
/// task o <c>Program.cs</c> chamava <c>AddAuthorization()</c> sem nenhuma política
/// registrada, e não existia um único <c>[Authorize(Roles=…)]</c>, <c>RequireClaim</c> ou
/// <c>AddPolicy</c> em todo o <c>.NET</c>: quem tinha token válido podia tudo que o token
/// alcançava. Papel existia no JWT (FD-03) e não era consultado por ninguém.
///
/// <para>
/// 🔴 <b>POR QUE É <c>RequireClaim</c> E NÃO <c>[Authorize(Roles=…)]</c>.</b> A claim de
/// papel emitida pela FD-03 chama-se <c>perfil</c>, que <b>não</b> é a claim de role padrão
/// do ASP.NET (<c>ClaimTypes.Role</c>,
/// <c>http://schemas.microsoft.com/ws/2008/06/identity/claims/role</c>). <c>Roles=</c>
/// resolve por <c>ClaimsPrincipal.IsInRole</c>, que lê o <c>RoleClaimType</c> da identidade
/// — então <c>[Authorize(Roles = "GESTOR")]</c> negaria <b>todo mundo</b> sem antes mapear
/// a claim.
/// </para>
///
/// <para>
/// Mapear era possível (<c>TokenValidationParameters.RoleClaimType = "perfil"</c>), e foi
/// <b>descartado</b> por raio de alcance: <c>RoleClaimType</c> é global do handler JWT e
/// muda o significado de <c>User.IsInRole</c> em todo o processo — inclusive em código que
/// hoje não existe e que passaria a herdar a semântica sem escolher. <c>RequireClaim</c>
/// afeta exatamente uma política, e o nome da claim aparece uma vez, ligado à mesma
/// constante que o <c>ClinicaContext</c> usa para ler (<see cref="ClinicaContext.ClaimPerfil"/>)
/// — se um lado renomear, o outro não continua compilando por acaso.
/// </para>
///
/// <para>
/// 🔴 <b>POR QUE ESTA POLÍTICA FALHA FECHADA, e por que isso não é detalhe.</b>
/// <c>IClinicaContext.Perfil</c> é <c>string?</c> e é <b>null para todo token emitido ANTES
/// da FD-03</b> — e esses tokens <b>continuam válidos até expirar</b>. A pergunta que decide
/// a segurança desta task é o que acontece com um token sem a claim <c>perfil</c>.
/// </para>
///
/// <para>
/// <c>RequireClaim(tipo, valores)</c> é uma <b>lista de permissão</b>: ele só concede quando
/// <b>acha</b> a claim com um dos valores. Claim ausente ⇒ nada é encontrado ⇒ o requisito
/// não é satisfeito ⇒ <b>403</b>. A ausência é negação por construção, não por comparação
/// que alguém precise lembrar de escrever.
/// </para>
///
/// <para>
/// ⚠️ <b>A formulação ERRADA que estava disponível, e que parece equivalente:</b> uma lista
/// de negação — <c>RequireAssertion(ctx =&gt; ctx.User.FindFirst("perfil")?.Value !=
/// PerfisUsuarioClinica.Veterinario)</c>, ou seja "não é veterinário, logo é gestor". Ela
/// concede acesso de GESTOR a todo token pré-FD-03, porque <c>null != "VETERINARIO"</c> é
/// <c>true</c>. As duas formas passam identicamente nos testes de GESTOR e de VETERINARIO;
/// elas só divergem no caso do token antigo — que é exatamente o caso real. Isso está
/// travado por medição em <c>PoliticaSomenteGestorTests</c>, que exercita a política
/// registrada aqui <b>e</b> a variante de lista de negação lado a lado, provando que o teste
/// do token antigo é o único que distingue as duas.
/// </para>
///
/// <para>
/// <b><c>RequireAuthenticatedUser()</c> é redundante hoje e está escrito de propósito.</b>
/// Nenhum principal anônimo carrega a claim <c>perfil</c>, então <c>RequireClaim</c> sozinho
/// já barraria — medido. Ele existe para que a política continue negando se algum dia a
/// aplicação passar a popular claims fora de um <c>ClaimsIdentity</c> autenticado (um
/// <c>ClaimsPrincipal</c> montado à mão em middleware, por exemplo). Mesmo raciocínio da
/// guarda de null explícita em <c>KuraDbContext.ApplyTenantFilters</c>: não depender de
/// trivia de framework para uma garantia de segurança.
/// </para>
/// </summary>
public static class AuthorizationExtensions
{
    public static IServiceCollection AddKuraAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PoliticasAutorizacao.SomenteGestor, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(ClinicaContext.ClaimPerfil, PerfisUsuarioClinica.Gestor));
        });

        return services;
    }
}
