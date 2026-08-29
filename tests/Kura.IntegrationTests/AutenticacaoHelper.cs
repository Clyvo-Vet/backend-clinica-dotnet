namespace Kura.IntegrationTests;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Kura.Application.DTOs.Auth;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Utilidades compartilhadas pelos cenários HTTP: obtenção de token pelo endpoint REAL
/// de login e forja de tokens inválidos para os controles negativos.
/// </summary>
internal static class AutenticacaoHelper
{
    /// <summary>
    /// Faz login de verdade em <c>POST /api/v1/auth/login</c> e devolve o token.
    /// Deliberadamente NÃO gera o token localmente: um token forjado provaria só que a
    /// validação do JWT funciona, não que o fluxo de autenticação da aplicação funciona.
    /// </summary>
    public static async Task<string> ObterTokenAsync(
        HttpClient client, string? email = null)
    {
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = email ?? KuraApiFactory.EmailClinica,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        resposta.EnsureSuccessStatusCode();

        var token = await resposta.Content.ReadFromJsonAsync<TokenResponseDto>();
        return token!.AccessToken;
    }

    public static void UsarToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Token assinado com a MESMA chave/emissor/audiência da aplicação, mas já expirado.
    /// Expira 2 horas no passado de propósito: o <c>ClockSkew</c> padrão do
    /// <c>JwtBearer</c> é de 5 minutos, então um token "expirado há 1 minuto" ainda
    /// seria aceito e o teste passaria por engano.
    /// </summary>
    public static string GerarTokenExpirado()
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KuraApiFactory.ChaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: KuraApiFactory.EmissorJwt,
            audience: KuraApiFactory.AudienciaJwt,
            claims:
            [
                new Claim("clinicaId", KuraApiFactory.IdClinicaSemeada.ToString()),
                new Claim("veterinarioId", KuraApiFactory.IdVeterinarioSemeado.ToString()),
            ],
            notBefore: DateTime.UtcNow.AddHours(-3),
            expires: DateTime.UtcNow.AddHours(-2),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 🔴 FD-04 — token <b>VÁLIDO</b> no formato ANTERIOR à FD-03: assinatura correta,
    /// emissor e audiência corretos, dentro da validade, com <c>clinicaId</c> e
    /// <c>veterinarioId</c> — e <b>SEM a claim <c>perfil</c></b>.
    ///
    /// <para>Não é um token forjado para inventar um cenário: é exatamente o que
    /// <c>AuthService.GenerateToken</c> emitia antes da FD-03, e todo token desse formato
    /// <b>continua sendo aceito pela autenticação até expirar</b> (o
    /// <c>Jwt:ExpiryHours</c> padrão é 8h). A pergunta que ele faz à política
    /// <c>SomenteGestor</c> é a que decide a segurança da FD-04: ausência de papel vira
    /// negação (403) ou vira permissão?</para>
    ///
    /// <para>⚠️ <b>Este é o único token desta suíte que não vem do endpoint de login</b>, e
    /// tem de ser forjado justamente porque o login de hoje é <b>incapaz</b> de emiti-lo — a
    /// FD-03 sempre põe a claim. Um teste que só usasse tokens do login não conseguiria
    /// alcançar este caso.</para>
    /// </summary>
    public static string GerarTokenPreFd03()
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KuraApiFactory.ChaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: KuraApiFactory.EmissorJwt,
            audience: KuraApiFactory.AudienciaJwt,
            claims:
            [
                new Claim("clinicaId", KuraApiFactory.IdClinicaSemeada.ToString()),
                new Claim("veterinarioId", KuraApiFactory.IdVeterinarioSemeado.ToString()),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A-5 (fix wave pos-G2 da FD-04) - token VALIDO com <c>perfil=GESTOR</c> e <b>sem</b> a
    /// claim <c>clinicaId</c>.
    ///
    /// <para>A politica <c>SomenteGestor</c> exige papel e NAO exige tenant, entao ela
    /// <b>aprova</b> este token. O que acontece depois e o que interessa: o primeiro acesso a
    /// <c>IClinicaContext.IdClinica</c> lanca <c>UnauthorizedAccessException</c>
    /// (<c>GetRequiredClaimValue</c>), que o <c>ExceptionHandlerMiddleware</c> mapeia para
    /// <b>401</b>. Ou seja, a lacuna degrada FECHADO - e isso e medido, nao deduzido, em
    /// <c>UsuariosClinicaHttpTests.Token_de_GESTOR_sem_clinicaId_degrada_fechado_em_401</c>.</para>
    /// </summary>
    public static string GerarTokenGestorSemClinicaId()
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KuraApiFactory.ChaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: KuraApiFactory.EmissorJwt,
            audience: KuraApiFactory.AudienciaJwt,
            claims: [new Claim("perfil", "GESTOR")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Token bem formado, com os mesmos claims, mas assinado com OUTRA chave — isola
    /// "assinatura inválida" de "token sintaticamente quebrado".
    /// </summary>
    public static string GerarTokenComAssinaturaInvalida()
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("chave-completamente-diferente-com-mais-de-32-bytes"));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: KuraApiFactory.EmissorJwt,
            audience: KuraApiFactory.AudienciaJwt,
            claims:
            [
                new Claim("clinicaId", KuraApiFactory.IdClinicaSemeada.ToString()),
                new Claim("veterinarioId", KuraApiFactory.IdVeterinarioSemeado.ToString()),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
