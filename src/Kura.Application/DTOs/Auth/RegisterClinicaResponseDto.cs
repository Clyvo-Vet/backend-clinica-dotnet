namespace Kura.Application.DTOs.Auth;

using Kura.Application.DTOs.Veterinario;

public sealed class RegisterClinicaResponseDto
{
    public long IdClinica { get; init; }
    public string NmClinica { get; init; } = string.Empty;
    public string DsEmailAcesso { get; init; } = string.Empty;
    public DateTime DtCriacao { get; init; }
    public long IdVeterinarioAdmin { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Papel do usuário criado junto com a clínica. É sempre <c>GESTOR</c> (FD-03: o
    /// registro cria o <c>USUARIO_CLINICA</c> gestor na mesma transação). Campo NOVO,
    /// acrescentado por simetria com <see cref="TokenResponseDto.TpPerfil"/> — nenhum
    /// consumidor existente o lê.
    /// </summary>
    public string TpPerfil { get; init; } = string.Empty;

    /// <summary>
    /// Aqui <b>continua não-nulo</b>, ao contrário de <see cref="TokenResponseDto.Usuario"/>:
    /// <c>RegisterClinicaAsync</c> cria o <c>Veterinario</c> administrador na mesma
    /// transação, então sempre existe ficha para devolver. <b>Não afrouxar</b> — tanto
    /// <c>seed-demo.sh:162</c> quanto <c>smoke-contratos.sh:251</c> leem <c>usuario.id</c>
    /// desta resposta e o usam como <c>idVeterinario</c> nos POSTs seguintes; um nulo aqui
    /// quebraria os dois scripts, em outro repositório.
    /// </summary>
    public VeterinarioResponseDto Usuario { get; init; } = null!;
}
