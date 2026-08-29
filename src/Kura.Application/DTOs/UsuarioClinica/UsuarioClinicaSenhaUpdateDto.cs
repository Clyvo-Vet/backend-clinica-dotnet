namespace Kura.Application.DTOs.UsuarioClinica;

/// <summary>
/// Definição de senha de um usuário da clínica, feita por um GESTOR autenticado
/// (<c>PUT /api/v1/usuarios-clinica/{id}/senha</c>).
///
/// <para>
/// ⚠️ <b>Isto NÃO é a "recuperação de senha" que o escopo negativo da FD-04 proíbe.</b> O que
/// está fora de escopo é o fluxo de <b>autosserviço</b>: link por e-mail, token de reset,
/// pergunta secreta — nada disso existe aqui. Este endpoint é administração de usuário por
/// alguém que <b>já provou ser GESTOR</b> daquela clínica, e existe porque sem ele o par
/// "criar usuário / usuário perdeu a senha" não tem saída nenhuma dentro do produto: não há
/// recuperação, não há convite por e-mail, e a única alternativa seria mexer no Oracle à mão.
/// </para>
/// </summary>
public sealed class UsuarioClinicaSenhaUpdateDto
{
    /// <summary>
    /// Senha em texto puro, apenas na entrada. Vira hash BCrypt no service; nunca é
    /// devolvida nem logada.
    /// </summary>
    public string DsSenha { get; init; } = string.Empty;
}
