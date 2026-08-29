namespace Kura.Application.DTOs.UsuarioClinica;

/// <summary>
/// Representação de saída de um usuário da clínica (FD-04).
///
/// <para>
/// 🔴 <b>NÃO existe campo de senha nem de hash aqui, e isso é requisito, não estilo.</b>
/// <c>DS_SENHA_HASH</c> é o segredo verificável da conta: devolvê-lo entrega ao cliente um
/// alvo de ataque offline (BCrypt é lento, não invulnerável) e, pior, um hash vazado
/// continua válido depois de qualquer troca de token. A garantia está travada por medição em
/// <c>UsuariosClinicaHttpTests.Resposta_nunca_carrega_hash_de_senha</c>, que inspeciona o
/// <b>JSON literal</b> da resposta — e não o tipo — porque um campo acrescentado por engano
/// num DTO novo passaria por qualquer asserção escrita sobre propriedades conhecidas.
/// </para>
/// </summary>
public sealed class UsuarioClinicaResponseDto
{
    public long Id { get; init; }

    /// <summary>
    /// Sempre a clínica do JWT — o service nunca aceita clínica vinda do corpo. Devolvido
    /// para que um cliente (e um teste) consiga afirmar em que tenant a linha caiu.
    /// </summary>
    public long IdClinica { get; init; }

    public long? IdVeterinario { get; init; }

    public string DsEmail { get; init; } = string.Empty;

    public string TpPerfil { get; init; } = string.Empty;

    public bool StAtiva { get; init; }

    public DateTime DtCriacao { get; init; }

    public DateTime? DtAtualizacao { get; init; }
}
