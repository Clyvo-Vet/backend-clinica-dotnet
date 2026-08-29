namespace Kura.IntegrationTests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services;

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
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailClinica,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        // Assert
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

        // FD-03: a credencial agora vive em USUARIO_CLINICA e o papel viaja na resposta.
        corpo.TpPerfil.Should().Be("VETERINARIO");
    }

    /// <summary>
    /// 🔴 <b>FD-03 — prova de mordida sobre HTTP real: GESTOR PURO.</b> Um usuário sem
    /// <c>ID_VETERINARIO</c> autentica, o token sai <b>sem</b> a claim <c>veterinarioId</c> e
    /// a chave <c>usuario</c> vem <c>null</c> no corpo — atravessando roteamento,
    /// <c>AuthService</c> real, EF e a serialização de produção.
    ///
    /// <para><b>Controle positivo:</b> contra o código antigo este login sequer chegava ao
    /// token — <c>LoginAsync</c> validava contra <c>CLINICA</c> e este e-mail não existe lá,
    /// então a resposta seria 422. E a asserção sobre o JSON CRU é o que separa "campo nulo"
    /// de "campo sumiu": se a serialização passar a omitir nulos, o app da clínica
    /// (<c>types/api.ts</c>) recebe outro shape e este teste pega.</para>
    /// </summary>
    [Fact]
    public async Task Login_de_gestor_sem_veterinario_devolve_200_com_usuario_nulo_e_sem_claim_de_veterinario()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailGestorPuro,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("usuario", out var usuario).Should().BeTrue(
            "a chave tem que continuar presente — o contrato do app da clínica depende do shape");
        usuario.ValueKind.Should().Be(JsonValueKind.Null);

        var corpo = await resposta.Content.ReadFromJsonAsync<TokenResponseDto>();
        corpo!.Usuario.Should().BeNull();
        corpo.TpPerfil.Should().Be("GESTOR");

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(corpo.AccessToken).Claims.ToList();
        claims.Should().Contain(c => c.Type == "perfil" && c.Value == "GESTOR");
        claims.Should().Contain(c => c.Type == "clinicaId");
        claims.Should().NotContain(c => c.Type == "veterinarioId",
            "claim ausente é a única codificação honesta de \"este usuário não é veterinário\"");
    }

    /// <summary>
    /// 🔴 <b>FD-03 — prova de mordida sobre HTTP real: E-MAIL EM 2 CLÍNICAS.</b> A UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c>, então duas linhas com o mesmo e-mail são estado legal do
    /// banco. O login falha com mensagem PRÓPRIA em vez de escolher um tenant.
    ///
    /// <para><b>Controle positivo:</b> a senha enviada é válida para AS DUAS linhas semeadas —
    /// é a situação exata em que um <c>FirstOrDefault()</c> devolveria 200 com o tenant certo
    /// em metade das vezes. O teste asserta que o <c>title</c> NÃO é "Email ou senha
    /// inválidos.": disfarçar isso de senha errada passaria num teste que só checasse o
    /// status.</para>
    /// </summary>
    [Fact]
    public async Task Login_com_email_em_duas_clinicas_devolve_422_com_mensagem_propria()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailAmbiguo,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var titulo = corpo.RootElement.GetProperty("title").GetString();
        titulo.Should().Be(AuthService.MensagemEmailAmbiguo);
        titulo.Should().NotBe("Email ou senha inválidos.",
            "ambiguidade de cadastro não pode se disfarçar de credencial errada");
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
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = "ninguem@kura.test",
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        // Mensagem genérica de propósito (não revela se o e-mail existe) — se alguém
        // trocar por "e-mail não cadastrado", este teste pega.
        corpo.RootElement.GetProperty("title").GetString().Should().Be("Email ou senha inválidos.");
        corpo.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 🔴 <b>F1 da fix wave pós-G2, sobre HTTP real — o vazamento cross-tenant que a revisão
    /// G2 mediu.</b> Um <c>USUARIO_CLINICA</c> da clínica 1 cujo <c>ID_VETERINARIO</c> aponta o
    /// veterinário da clínica 2 loga normalmente, e a ficha do OUTRO tenant <b>não</b> sai no
    /// corpo do <c>200</c>.
    ///
    /// <para><b>Controle positivo, e ele é o ponto do teste:</b> o veterinário do outro tenant
    /// EXISTE no banco desta fábrica e tem nome distinto
    /// (<c>NomeVeterinarioOutroTenant</c>) — o teste asserta que esse nome NÃO aparece em
    /// lugar nenhum do JSON cru. Sem a guarda em <c>ObterVeterinarioVinculadoAsync</c> ele
    /// aparece, junto de CRMV, e-mail e telefone.</para>
    /// </summary>
    [Fact]
    public async Task Login_de_usuario_com_vinculo_em_outra_clinica_nao_vaza_a_ficha_do_outro_tenant()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = KuraApiFactory.EmailVinculoCruzado,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var bruto = await resposta.Content.ReadAsStringAsync();
        bruto.Should().NotContain(KuraApiFactory.NomeVeterinarioOutroTenant,
            "a ficha do veterinário da outra clínica não pode sair num 200 emitido para esta");

        var corpo = await resposta.Content.ReadFromJsonAsync<TokenResponseDto>();
        corpo!.Usuario.Should().BeNull();

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(corpo.AccessToken).Claims.ToList();
        claims.Should().Contain(
            c => c.Type == "clinicaId" && c.Value == KuraApiFactory.IdClinicaSemeada.ToString(),
            "o token segue escopado na clínica do usuário — o vazamento seria no CORPO");
    }

    /// <summary>
    /// 🔴 <b>F3 da fix wave pós-G2 — o round-trip registro → login, sobre HTTP real.</b> É o
    /// análogo, dentro deste repo, do que <c>seed-demo.sh</c> faz em outro:
    /// <c>POST /auth/register-clinica</c> (<c>:160</c>) e, depois,
    /// <c>POST /auth/login</c> com a MESMA credencial (<c>:321</c>).
    ///
    /// <para><b>Por que ele foi acrescentado mesmo com a mutação de F3 já mordendo:</b> a
    /// mutação (<c>ID_VETERINARIO = null</c> no registro) é pega por 2 testes UNITÁRIOS, e
    /// nenhum deles atravessa HTTP — a suíte de integração ficou <b>23/23 verde</b> sob ela,
    /// porque <c>KuraApiFactory</c> semeia o <c>USUARIO_CLINICA</c> à mão em vez de passar
    /// pelo registro. Ou seja: o caminho que o gate da FD-12 vai exercitar de verdade não
    /// tinha cobertura nenhuma aqui. Agora tem.</para>
    ///
    /// <para><b>Controle positivo:</b> o teste não se contenta com o <c>200</c> — ele casa o
    /// <c>usuario.id</c> da resposta de LOGIN com o <c>idVeterinarioAdmin</c> devolvido pelo
    /// REGISTRO. Um login que autenticasse "alguém" da clínica passaria num teste que só
    /// olhasse o status.</para>
    /// </summary>
    [Fact]
    public async Task Registro_de_clinica_seguido_de_login_funciona_como_no_seed_demo()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string email = "clinica-recem-registrada@kura.test";
        const string senha = "SenhaDeRegistro#2026";

        // Act 1 — registro (equivalente a seed-demo.sh:160).
        var registro = await client.PostAsJsonAsync("/api/v1/auth/register-clinica", new
        {
            nmClinica = "Clinica Recem Registrada",
            nrCnpj = "11.222.333/0001-81",
            dsEndereco = "Rua do Registro, 1",
            nmCidade = "Sao Paulo",
            sgUf = "SP",
            nrCep = "01000-000",
            nrTelefone = "11999990002",
            dsEmail = "contato-recem@kura.test",
            dsEmailAcesso = email,
            dsSenha = senha,
            nmVeterinarioAdmin = "Dr. Recem Registrado",
            nrCRMV = "CRMV-REG-0001",
        });

        registro.StatusCode.Should().Be(HttpStatusCode.Created);
        var corpoRegistro = await registro.Content.ReadFromJsonAsync<RegisterClinicaResponseDto>();
        corpoRegistro!.Usuario.Should().NotBeNull(
            "seed-demo.sh:162 e smoke-contratos.sh:251 leem `usuario.id` DESTA resposta");
        corpoRegistro.IdVeterinarioAdmin.Should().Be(corpoRegistro.Usuario.Id);

        // Act 2 — login com a mesma credencial (equivalente a seed-demo.sh:321).
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = email,
            dsSenha = senha,
        });

        // Assert — sem o USUARIO_CLINICA criado pelo registro, isto seria 422: a conversão da
        // V17 converte ZERO linhas num ambiente do zero.
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpoLogin = await login.Content.ReadFromJsonAsync<TokenResponseDto>();
        corpoLogin!.TpPerfil.Should().Be("GESTOR");
        corpoLogin.Usuario.Should().NotBeNull(
            "o gestor criado no registro TEM vínculo com o veterinário administrador — " +
            "é isso que mantém o app da clínica funcionando sem alteração");
        corpoLogin.Usuario!.Id.Should().Be(corpoRegistro.IdVeterinarioAdmin);

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(corpoLogin.AccessToken).Claims.ToList();
        claims.Should().Contain(
            c => c.Type == "veterinarioId"
                 && c.Value == corpoRegistro.IdVeterinarioAdmin.ToString());
        claims.Should().Contain(
            c => c.Type == "clinicaId" && c.Value == corpoRegistro.IdClinica.ToString());
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_devolve_401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_protegido_com_token_sintaticamente_invalido_devolve_401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.UsarToken("isto-nao-e-um-jwt");

        // Act
        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_protegido_com_token_expirado_devolve_401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenExpirado());

        // Act
        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // Assert
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
        // Arrange
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenComAssinaturaInvalida());

        // Act
        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
