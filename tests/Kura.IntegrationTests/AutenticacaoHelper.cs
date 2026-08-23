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
    public static async Task<string> ObterTokenAsync(HttpClient client)
    {
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailClinica,
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
