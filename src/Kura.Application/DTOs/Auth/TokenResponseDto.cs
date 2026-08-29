namespace Kura.Application.DTOs.Auth;

using Kura.Application.DTOs.Veterinario;

/// <summary>
/// Resposta de <c>POST /api/v1/auth/login</c>.
///
/// <para>🔴 <b>Contrato HTTP congelado.</b> As chaves <c>accessToken</c>, <c>expiresAt</c> e
/// <c>usuario</c> têm consumidores em OUTROS repositórios — <c>mobile-clinica-rn</c>
/// (<c>types/api.ts::LoginResponse</c>), <c>DevOps-Cloud/scripts/smoke-contratos.sh</c> e
/// <c>DevOps-Cloud/scripts/seed-demo.sh</c>. Nenhuma pode ser renomeada ou removida; só é
/// seguro ACRESCENTAR (JSON desconhecido é ignorado pelos três).</para>
/// </summary>
public sealed class TokenResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Papel do usuário autenticado — <c>GESTOR</c> ou <c>VETERINARIO</c>
    /// (<c>PerfisUsuarioClinica</c>). <b>Campo NOVO da FD-03</b>, acrescentado (nunca
    /// substituindo nada) para que <see cref="Usuario"/> nulo seja INTERPRETÁVEL pelo
    /// cliente: sem ele, <c>"usuario": null</c> é indistinguível de erro do servidor.
    /// </summary>
    public string TpPerfil { get; init; } = string.Empty;

    /// <summary>
    /// Ficha do veterinário vinculado ao usuário logado, ou <b><c>null</c></b> quando o
    /// usuário é um GESTOR sem registro em <c>VETERINARIO</c>.
    ///
    /// <para>🔴 <b>Passou a ser nullable na FD-03.</b> Antes nunca era nulo porque
    /// <c>LoginAsync</c> escolhia um veterinário por heurística de fallback (bate o e-mail,
    /// senão o primeiro por <c>Id</c>) — ou lançava "Clínica sem veterinário responsável
    /// cadastrado.". Morta a heurística, um gestor não-veterinário simplesmente <b>não tem</b>
    /// o que pôr aqui, e inventar um seria reintroduzir o defeito por outra porta: autoria
    /// ERRADA é estritamente pior que autoria ausente.</para>
    ///
    /// <para><b>Por que <c>null</c> e não omitir a chave:</b> a serialização padrão do
    /// projeto emite <c>"usuario": null</c>, então a chave continua presente e o FORMATO da
    /// resposta não muda para nenhum consumidor — muda o VALOR, num caso que não ocorre em
    /// ambiente de demonstração (ver <c>AuthService.RegisterClinicaAsync</c>, que vincula o
    /// gestor ao veterinário administrador que ele mesmo cria).</para>
    ///
    /// <para><b>Impacto medido no <c>mobile-clinica-rn</c>, que NÃO é alterado por esta
    /// task</b> (repo do backlog irmão): o app guarda a resposta em
    /// <c>authStore.usuario</c>, já tipado <c>VeterinarioResponse | null</c>; as telas de
    /// consulta e receituário fazem <c>if (!petId || !usuario) return;</c> — degradam em
    /// silêncio, não quebram —, e <c>settings</c>/<c>dashboard</c> já usam <c>?.</c> com
    /// fallback <c>'—'</c>. O app <b>não decodifica o JWT</b> (nenhuma biblioteca de decode
    /// em <c>src/</c>), então nenhuma claim nova ou ausente o afeta.</para>
    /// </summary>
    public VeterinarioResponseDto? Usuario { get; init; }
}
