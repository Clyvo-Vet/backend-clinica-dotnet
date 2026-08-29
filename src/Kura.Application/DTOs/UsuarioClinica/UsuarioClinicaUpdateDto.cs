namespace Kura.Application.DTOs.UsuarioClinica;

/// <summary>
/// Atualização de um usuário da clínica (FD-04). Sem <c>IdClinica</c> pelo mesmo motivo do
/// <see cref="UsuarioClinicaCreateDto"/>: usuário não muda de clínica, e o campo inexistente
/// não pode ser esquecido numa comparação.
///
/// <para>Sem senha aqui de propósito — troca de senha tem endpoint próprio
/// (<c>PUT /{id}/senha</c>, <see cref="UsuarioClinicaSenhaUpdateDto"/>), para que um
/// <c>PUT</c> de dados cadastrais nunca carregue segredo no corpo.</para>
/// </summary>
public sealed class UsuarioClinicaUpdateDto
{
    public string DsEmail { get; init; } = string.Empty;

    /// <summary>
    /// <c>GESTOR</c> ou <c>VETERINARIO</c>. ⚠️ Rebaixar o <b>último</b> GESTOR ativo da
    /// clínica é recusado com <c>422</c> — ver <c>UsuarioClinicaService</c>.
    /// </summary>
    public string TpPerfil { get; init; } = string.Empty;

    /// <summary>
    /// Vínculo com <c>VETERINARIO</c>. <c>null</c> remove o vínculo. Quando informado, o
    /// veterinário tem de ser da mesma clínica do JWT.
    /// </summary>
    public long? IdVeterinario { get; init; }
}
