namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kura.Application.DTOs.Auth;

/// <summary>
/// S3D-06 — item da rubrica: "fluxo completo de requisições HTTP, incluindo
/// AUTENTICAÇÃO". Requisições HTTP reais contra o <c>Program.cs</c> de produção,
/// atravessando roteamento, model binding, autenticação JWT, autorização, os services
/// reais e o EF (InMemory). Nenhum mock de <c>IAuthService</c>.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AutenticacaoHttpTests
{
    private readonly KuraApiFactory _factory;

    public AutenticacaoHttpTests(KuraApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_com_credenciais_validas_devolve_200_e_token()
    {
        var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailClinica,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await resposta.Content.ReadFromJsonAsync<TokenResponseDto>();
        corpo.Should().NotBeNull();
        corpo!.AccessToken.Should().NotBeNullOrWhiteSpace();
        // Três segmentos separados por ponto = JWT compacto; garante que veio um token
        // de verdade e não uma string qualquer.
        corpo.AccessToken.Split('.').Should().HaveCount(3);
        corpo.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        corpo.Usuario.Should().NotBeNull();
        corpo.Usuario!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        corpo.Usuario.NmVeterinario.Should().Be(KuraApiFactory.NomeVeterinarioSemeado);
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_422_com_corpo_de_erro_padrao()
    {
        var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailClinica,
            dsSenha = "senha-errada-de-proposito",
        });

        // RegraDeNegocioException -> 422, mapeado pelo ExceptionHandlerMiddleware.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var raiz = corpo.RootElement;

        raiz.GetProperty("type").GetString().Should().Be("RegraDeNegocioException");
        raiz.GetProperty("status").GetInt32().Should().Be(422);
        raiz.GetProperty("title").GetString().Should().Be("Email ou senha inválidos.");
        raiz.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_com_email_inexistente_devolve_422_com_a_mesma_mensagem_generica()
    {
        var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = "ninguem@kura.test",
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        // Mensagem genérica de propósito (não revela se o e-mail existe) — se alguém
        // trocar por "e-mail não cadastrado", este teste pega.
        corpo.RootElement.GetProperty("title").GetString().Should().Be("Email ou senha inválidos.");
        corpo.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_devolve_401()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_protegido_com_token_sintaticamente_invalido_devolve_401()
    {
        var client = _factory.CreateClient();
        client.UsarToken("isto-nao-e-um-jwt");

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_protegido_com_token_expirado_devolve_401()
    {
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenExpirado());

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // O JwtBearer publica o motivo no header WWW-Authenticate; asserir isso separa
        // "expirou" de "assinatura errada" e impede que o teste passe por acidente.
        // Texto MEDIDO nesta versao do JwtBearer (10.0.7):
        //   Bearer error="invalid_token", error_description="The token expired at '...'"
        var motivo = resposta.Headers.WwwAuthenticate.ToString();
        motivo.Should().Contain("invalid_token");
        motivo.Should().Contain("The token expired at");
    }

    [Fact]
    public async Task Endpoint_protegido_com_assinatura_invalida_devolve_401()
    {
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenComAssinaturaInvalida());

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
