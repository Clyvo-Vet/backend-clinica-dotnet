namespace Kura.Application.DTOs.UsuarioClinica;

/// <summary>
/// Criação de um usuário da clínica (FD-04).
///
/// <para>
/// 🔴 <b>NÃO EXISTE <c>IdClinica</c> AQUI, e a ausência é a defesa.</b> A clínica do usuário
/// criado sai do <c>clinicaId</c> do JWT (<c>IClinicaContext.IdClinica</c>), sempre. A
/// alternativa — aceitar o campo e comparar com o JWT no service — é exatamente a forma que
/// a <c>FD-05</c> documenta como já quebrada em produção: <c>VeterinarioService.CreateAsync</c>
/// grava <c>dto.IdClinica</c> sem comparar com o token, e por isso qualquer clínica
/// autenticada cria veterinário dentro de outra. Um campo que não existe não pode ser
/// esquecido numa comparação; um campo que existe depende de alguém lembrar, para sempre,
/// em todo caminho novo.
/// </para>
///
/// <para>
/// Consequência declarada: um cliente que mande <c>"idClinica": 2</c> no corpo <b>não recebe
/// erro</b> — o binder de modelo do ASP.NET ignora propriedade desconhecida por padrão, e o
/// usuário nasce na clínica do token. Isso é deliberado: rejeitar exigiria um campo só para
/// ser recusado. Provado por HTTP em
/// <c>UsuariosClinicaHttpTests.Criar_usuario_ignora_idClinica_do_corpo_e_usa_a_do_jwt</c>.
/// </para>
/// </summary>
public sealed class UsuarioClinicaCreateDto
{
    /// <summary>E-mail de login. Único POR CLÍNICA (<c>UK_USUARIO_CLINICA_EMAIL</c>).</summary>
    public string DsEmail { get; init; } = string.Empty;

    /// <summary>
    /// Senha em texto puro, apenas na entrada. É convertida em hash BCrypt no service e
    /// <b>nunca</b> é devolvida, ecoada nem logada — a resposta é
    /// <see cref="UsuarioClinicaResponseDto"/>, que não tem campo de senha nem de hash.
    /// </summary>
    public string DsSenha { get; init; } = string.Empty;

    /// <summary><c>GESTOR</c> ou <c>VETERINARIO</c> (<c>PerfisUsuarioClinica</c>).</summary>
    public string TpPerfil { get; init; } = string.Empty;

    /// <summary>
    /// Vínculo opcional com um registro de <c>VETERINARIO</c>. Quando informado, o
    /// veterinário tem de ser da <b>mesma clínica</b> do JWT — a
    /// <c>FK_USUARIO_CLINICA_VET</c> da V17 referencia só <c>VETERINARIO(ID_VETERINARIO)</c>,
    /// <b>sem compor com <c>ID_CLINICA</c></b>, então o banco aceita o cruzamento e a única
    /// defesa é código.
    /// </summary>
    public long? IdVeterinario { get; init; }
}
