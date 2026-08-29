namespace Kura.Domain.Entities;

/// <summary>
/// Identidade individual do lado clínico (FD-02, ciclo FIN). Uma linha por HUMANO
/// da clínica — login próprio e papel próprio —, em oposição ao login POR CLÍNICA
/// que existe hoje em <c>CLINICA.DS_EMAIL_ACESSO</c>/<c>CLINICA.DS_SENHA_HASH</c>
/// (<c>AuthService.LoginAsync</c>), onde a clínica inteira compartilha um par
/// e-mail/senha e o veterinário "logado" é escolhido por heurística de fallback.
///
/// <para><b>Tabela .NET-owned.</b> Criada pelo Flyway em
/// <c>V17__usuario_clinica.sql</c> (repo <c>backend-tutor-java</c>, única autoridade
/// de DDL do projeto). As propriedades abaixo espelham aquele <c>CREATE TABLE</c>
/// coluna a coluna; nomes e dimensões vivem em <c>UsuarioClinicaConfiguration</c>.</para>
///
/// <para><b><see cref="IdVeterinario"/> é nullable de propósito.</b> Um GESTOR pode
/// não ser veterinário (dono, administrador, financeiro). O vínculo, quando existe,
/// é EXPLÍCITO — nunca derivado por e-mail: casar
/// <c>CLINICA.DS_EMAIL_ACESSO</c> com <c>VETERINARIO.DS_EMAIL</c> seria a mesma
/// classe de heurística que este ciclo está eliminando, e produziria autoria
/// ERRADA (não ausente), que é estritamente pior. Ver o cabeçalho da V17.</para>
///
/// <para><b>Unicidade de e-mail é POR CLÍNICA, não global</b>
/// (<c>UK_USUARIO_CLINICA_EMAIL (ID_CLINICA, DS_EMAIL)</c>) — um veterinário que
/// atende em duas clínicas é o caso real. Consequência para a FD-03: o login não
/// pode resolver o usuário só pelo e-mail.</para>
///
/// <para><b>Escopo desta task (FD-02):</b> domínio, mapeamento e isolamento de
/// tenant. Autenticação, claim de papel e a morte da heurística de fallback são a
/// FD-03; CRUD/endpoint é a FD-04.</para>
/// </summary>
public class UsuarioClinica : EntidadeBase
{
    /// <summary>
    /// <c>ID_CLINICA</c> — NOT NULL na V17. Não existe usuário sem tenant: é a chave
    /// do isolamento multi-tenant (ver <c>KuraDbContext.ApplyTenantFilters</c>).
    /// </summary>
    public long IdClinica { get; set; }

    /// <summary>
    /// <c>ID_VETERINARIO</c> — NULLABLE na V17. Preenchido só quando o usuário for,
    /// de fato, um veterinário com registro em <c>VETERINARIO</c>. NULL para gestor
    /// que não atende.
    /// </summary>
    public long? IdVeterinario { get; set; }

    /// <summary>E-mail de login. Único por clínica, não globalmente.</summary>
    public string DsEmail { get; set; } = string.Empty;

    /// <summary>
    /// Hash BCrypt da senha individual. Dimensionado em paridade com
    /// <c>CLINICA.DS_SENHA_HASH</c> (256) para a conversão da V17 não truncar — um
    /// hash BCrypt truncado não falha o INSERT, falha o LOGIN, muito depois.
    /// </summary>
    public string DsSenhaHash { get; set; } = string.Empty;

    /// <summary>
    /// Papel do usuário. Valores aceitos pelo banco estão travados por
    /// <c>CHK_USUARIO_CLINICA_PERFIL</c> (V17) — ver <see cref="PerfisUsuarioClinica"/>.
    /// Mantido como <c>string</c> (e não enum) para espelhar a coluna
    /// <c>VARCHAR2(20)</c> e para que acrescentar um papel futuro (ex.:
    /// RECEPCIONISTA) seja mexer na constraint, não no tipo do modelo.
    /// </summary>
    public string TpPerfil { get; set; } = string.Empty;
}

/// <summary>
/// Valores de <see cref="UsuarioClinica.TpPerfil"/> aceitos hoje por
/// <c>CHK_USUARIO_CLINICA_PERFIL</c> (V17__usuario_clinica.sql). Constantes, e não
/// enum, porque a autoridade sobre o conjunto é a constraint do banco: acrescentar
/// um papel é um <c>ALTER TABLE ... DROP/ADD CONSTRAINT</c> em nova migration
/// Flyway, e nada mais no schema.
/// </summary>
public static class PerfisUsuarioClinica
{
    public const string Gestor = "GESTOR";
    public const string Veterinario = "VETERINARIO";
}
