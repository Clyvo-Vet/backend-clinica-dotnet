namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.DTOs.UsuarioClinica;
using Kura.Domain.Entities;

/// <summary>
/// FD-04 — a política <c>SomenteGestor</c> e o CRUD de <c>USUARIO_CLINICA</c> sobre HTTP
/// real, com o <c>Program.cs</c> de produção (autenticação JWT, pipeline de middlewares,
/// FluentValidation, <c>ExceptionHandlerMiddleware</c>).
///
/// <para>
/// <b>Por que <c>IClassFixture</c> e não a <c>ColecaoDeIntegracao</c>:</b> esta classe
/// <b>escreve</b> em <c>USUARIO_CLINICA</c>, que é a tabela de onde o login das outras duas
/// classes tira as credenciais. Compartilhar o banco InMemory com elas faria um teste daqui
/// poder derrubar o login de lá conforme a ordem de execução — que o xUnit não garante. O
/// custo é um bootstrap de host a mais, que roda em paralelo por estar em outra collection
/// (mesma decisão, e mesmo argumento, de <see cref="AmbienteEFiacaoDoHostTests"/>).
/// </para>
///
/// <para>
/// ⚠️ <b>Os testes desta classe compartilham UM banco entre si</b>, e a ordem entre eles não
/// é garantida. Por isso todo usuário criado aqui usa e-mail único por
/// <see cref="Guid"/>, e nenhuma asserção depende de cardinalidade da lista. A única exceção
/// é <see cref="Desativar_o_ultimo_gestor_da_clinica_devolve_422"/>, que precisa controlar a
/// contagem de gestores e por isso sobe uma <b>fábrica própria</b>, descartada no fim.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class UsuariosClinicaHttpTests : IClassFixture<KuraApiFactory>
{
    private const string Rota = "/api/v1/usuarios-clinica";

    private readonly KuraApiFactory _factory;

    public UsuariosClinicaHttpTests(KuraApiFactory factory) => _factory = factory;

    /// <summary>
    /// Cliente com token de <b>GESTOR</b> — obtido pelo endpoint REAL de login, como
    /// <c>EmailGestorPuro</c> (o usuário GESTOR sem vínculo de veterinário semeado pela
    /// FD-03). Token forjado provaria a validação do JWT, não o fluxo.
    /// </summary>
    private async Task<HttpClient> ClienteGestorAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));
        return client;
    }

    private static object CorpoDeCriacao(
        string email,
        string perfil = PerfisUsuarioClinica.Veterinario,
        long? idVeterinario = null) => new
        {
            dsEmail = email,
            dsSenha = KuraApiFactory.SenhaClinica,
            tpPerfil = perfil,
            idVeterinario,
        };

    private static string EmailUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}@kura.test";

    // ─────────────────────────────────────────────────────────────────────────────────────
    // A política SomenteGestor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_token_devolve_401()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync(Rota);

        // 401, e não 403: sem credencial nenhuma o pipeline DESAFIA a autenticação antes de
        // chegar à autorização. A distinção importa — 403 aqui indicaria que o endpoint
        // aceitou o anônimo como autenticado.
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_de_perfil_VETERINARIO_devolve_403()
    {
        // O usuário semeado em EmailClinica tem TpPerfil = VETERINARIO (KuraApiFactory).
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var resposta = await client.GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_de_perfil_GESTOR_devolve_200()
    {
        // 🔴 Controle positivo dos dois testes acima: sem ele, um endpoint que negasse TODO
        // mundo (política quebrada, rota errada, controller não registrado) passaria nos dois.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await resposta.Content.ReadFromJsonAsync<List<UsuarioClinicaResponseDto>>();
        lista.Should().NotBeNull();
        lista!.Should().OnlyContain(u => u.IdClinica == KuraApiFactory.IdClinicaSemeada);
        // O outro tenant TEM usuários (KuraApiFactory semeia 2 lá). A asserção de escopo
        // acima é, portanto, capaz de falhar.
        lista.Should().Contain(u => u.DsEmail == KuraApiFactory.EmailGestorPuro);
    }

    [Fact]
    public async Task Token_pre_FD03_sem_a_claim_perfil_devolve_403()
    {
        // 🔴 O TESTE QUE DECIDE A SEGURANÇA DESTA TASK. Token assinado com a chave real,
        // dentro da validade, no formato que AuthService emitia ANTES da FD-03 — sem a claim
        // `perfil`. Tokens desse formato continuam sendo aceitos pela autenticação até
        // expirar. A política tem de tratar papel AUSENTE como negação.
        //
        // Se ela fosse escrita como lista de negação ("não é VETERINARIO, logo é GESTOR"),
        // este mesmo token receberia 200 — ver
        // PoliticaSomenteGestorTests.A_formulacao_por_lista_de_negacao_concederia_acesso_ao_token_antigo,
        // que mede as duas formulações lado a lado.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.GetAsync(Rota);

        // 403 e não 401: o token É válido e o usuário ESTÁ autenticado. O que falta é papel.
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_pre_FD03_e_aceito_pela_AUTENTICACAO_em_endpoint_sem_a_politica()
    {
        // Controle positivo do teste acima, e o que torna o 403 dele interpretável: o mesmo
        // token forjado devolve 200 num endpoint autenticado SEM a política. Ou seja, o 403
        // vem da política — não de assinatura inválida, emissor errado ou token expirado, que
        // dariam 401 e passariam despercebidos numa asserção de "não é 200".
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_de_VETERINARIO_e_barrado_em_TODOS_os_verbos_do_controller()
    {
        // A política está no CONTROLLER, não método a método. Este teste é o que trava essa
        // decisão: mover para método a método e esquecer um deixaria o endpoint esquecido
        // respondendo normalmente para um veterinário.
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var respostas = new[]
        {
            await client.GetAsync(Rota),
            await client.GetAsync($"{Rota}/1"),
            await client.PostAsJsonAsync(Rota, CorpoDeCriacao(EmailUnico("vet-tentou"))),
            await client.PutAsJsonAsync($"{Rota}/1", new
            {
                dsEmail = "x@kura.test",
                tpPerfil = PerfisUsuarioClinica.Gestor,
            }),
            await client.PutAsJsonAsync($"{Rota}/1/senha", new { dsSenha = "OutraSenha#2026" }),
            await client.PostAsJsonAsync($"{Rota}/1/reativacao", new { }),
            await client.DeleteAsync($"{Rota}/1"),
        };

        respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // CRUD — escopo de escrita, unicidade, vínculo, segredo
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_usuario_ignora_idClinica_do_corpo_e_usa_a_do_jwt()
    {
        // 🔴 A prova de mordida da FD-05 aplicada aqui: o corpo carrega idClinica do OUTRO
        // tenant. Como UsuarioClinicaCreateDto não tem esse campo, o binder o descarta e a
        // clínica vem do JWT. O teste manda o campo de propósito — um DTO que ganhasse
        // IdClinica no futuro faria este teste começar a gravar no tenant errado e falhar.
        var client = await ClienteGestorAsync();
        var email = EmailUnico("corpo-com-clinica");

        var resposta = await client.PostAsJsonAsync(Rota, new
        {
            dsEmail = email,
            dsSenha = KuraApiFactory.SenhaClinica,
            tpPerfil = PerfisUsuarioClinica.Veterinario,
            idClinica = KuraApiFactory.IdClinicaOutroTenant,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await resposta.Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();
        criado!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        criado.IdClinica.Should().NotBe(KuraApiFactory.IdClinicaOutroTenant);

        // Confirmação por leitura, e não só pelo eco da criação: o usuário é alcançável pelo
        // token da clínica do JWT — logo a linha está mesmo neste tenant.
        var leitura = await client.GetAsync($"{Rota}/{criado.Id}");
        leitura.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Criar_usuario_com_email_repetido_na_mesma_clinica_devolve_422_tratado()
    {
        var client = await ClienteGestorAsync();
        var email = EmailUnico("repetido");

        var primeira = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(email));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(email));

        // 422 do ExceptionHandlerMiddleware (RegraDeNegocioException) — erro de negócio
        // tratado, e não o 500 que a violação de UK_USUARIO_CLINICA_EMAIL produziria contra o
        // Oracle. ⚠️ Este cenário NÃO seria pego pelo banco nesta suíte: o provider InMemory
        // não valida índice único, então sem a checagem explícita no service a segunda
        // chamada devolveria 201.
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var corpo = await segunda.Content.ReadAsStringAsync();
        corpo.Should().Contain("RegraDeNegocioException");
    }

    [Fact]
    public async Task Criar_usuario_vinculando_veterinario_de_OUTRA_clinica_devolve_422()
    {
        // A FK_USUARIO_CLINICA_VET da V17 não compõe com ID_CLINICA: o Oracle aceitaria esta
        // linha. A única defesa é código — e é ela que este teste morde.
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(
            EmailUnico("vinculo-cruzado"),
            PerfisUsuarioClinica.Veterinario,
            KuraApiFactory.IdVeterinarioOutroTenant));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Criar_usuario_vinculando_veterinario_da_PROPRIA_clinica_devolve_201()
    {
        // Controle positivo do teste acima: sem ele, um service que recusasse todo vínculo
        // passaria igual.
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(
            EmailUnico("vinculo-ok"),
            PerfisUsuarioClinica.Veterinario,
            KuraApiFactory.IdVeterinarioSemeado));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await resposta.Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();
        criado!.IdVeterinario.Should().Be(KuraApiFactory.IdVeterinarioSemeado);
    }

    [Fact]
    public async Task Resposta_nunca_carrega_hash_de_senha()
    {
        var client = await ClienteGestorAsync();
        var email = EmailUnico("sem-hash");

        var criacao = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(email));
        var jsonCriacao = await criacao.Content.ReadAsStringAsync();
        var lista = await (await client.GetAsync(Rota)).Content.ReadAsStringAsync();

        foreach (var json in new[] { jsonCriacao, lista })
        {
            // Controle positivo do instrumento: se a busca não achasse NADA, um corpo vazio
            // (ou uma resposta de erro) passaria em todas as asserções negativas abaixo.
            json.Should().Contain("dsEmail", "a inspeção precisa estar olhando um corpo real");

            json.Should().NotContain("senhaHash");
            json.Should().NotContain("dsSenha");
            // Prefixo de hash BCrypt em qualquer variante ($2a$, $2b$, $2y$): pega o valor
            // mesmo que ele vaze sob um nome de campo diferente do esperado.
            json.Should().NotContain("$2");
            json.Should().NotContain(KuraApiFactory.SenhaClinica);
        }
    }

    [Fact]
    public async Task Usuario_criado_consegue_logar_com_a_senha_definida()
    {
        // Prova ponta a ponta de que o hash BCrypt gravado é o que o login verifica. Um hash
        // truncado, ou gerado por outro algoritmo, passaria por toda asserção sobre o corpo
        // da criação e falharia SÓ aqui — que é onde o usuário real descobriria.
        var client = await ClienteGestorAsync();
        var email = EmailUnico("faz-login");

        var criacao = await client.PostAsJsonAsync(
            Rota, CorpoDeCriacao(email, PerfisUsuarioClinica.Gestor));
        criacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var anonimo = _factory.CreateClient();
        var login = await anonimo.PostAsJsonAsync("/api/v1/auth/login", new
        {
            dsEmail = email,
            dsSenha = KuraApiFactory.SenhaClinica,
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await login.Content.ReadFromJsonAsync<TokenResponseDto>();
        token!.TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);

        // E o token nasce com papel de verdade: o usuário recém-criado passa pela política.
        anonimo.UsarToken(token.AccessToken);
        (await anonimo.GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Buscar_usuario_de_outra_clinica_devolve_404()
    {
        // O id 2 é o usuário semeado no OUTRO tenant (KuraApiFactory). 404, e não 403: a
        // resposta é indistinguível de "não existe", então o endpoint não vira oráculo de
        // existência de id alheio.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync($"{Rota}/2");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Controle positivo: o mesmo verbo, com um id da própria clínica, devolve 200.
        (await client.GetAsync($"{Rota}/3")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Criar_usuario_com_perfil_desconhecido_devolve_400_de_contrato()
    {
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota, CorpoDeCriacao(EmailUnico("perfil-ruim"), "RECEPCIONISTA"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------------------
    // A-1 / A-3 / A-5 (fix wave pos-G2)
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Atualizar_usuario_de_outra_clinica_devolve_404()
    {
        // A-1: o unico verbo que nao tinha teste de escopo de tenant. O id 2 e o usuario
        // semeado no OUTRO tenant.
        var client = await ClienteGestorAsync();

        var resposta = await client.PutAsJsonAsync($"{Rota}/2", new
        {
            dsEmail = "invadido@kura.test",
            tpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Controle positivo: o MESMO verbo, com id da propria clinica, nao devolve 404.
        var proprio = await client.PutAsJsonAsync($"{Rota}/3", new
        {
            dsEmail = KuraApiFactory.EmailGestorPuro,
            tpPerfil = PerfisUsuarioClinica.Gestor,
        });
        proprio.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Usuario_desativado_recusa_PUT_com_422_e_volta_pela_reativacao()
    {
        // A-3, o ciclo inteiro sobre HTTP: antes da fix wave, o PUT sobre usuario desativado
        // respondia 200 sem reativar - sucesso silencioso, a classe de defeito da TASK-69.
        var client = await ClienteGestorAsync();
        var email = EmailUnico("porta-de-volta");

        var criacao = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(email));
        var criado = await criacao.Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();

        (await client.DeleteAsync($"{Rota}/{criado!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var putEnquantoInativo = await client.PutAsJsonAsync($"{Rota}/{criado.Id}", new
        {
            dsEmail = email,
            tpPerfil = PerfisUsuarioClinica.Gestor,
        });
        putEnquantoInativo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await putEnquantoInativo.Content.ReadAsStringAsync())
            .Should().Contain("DESATIVADO");

        // A gravacao nao pode ter acontecido junto com a recusa.
        var apurado = await (await client.GetAsync($"{Rota}/{criado.Id}"))
            .Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();
        apurado!.StAtiva.Should().BeFalse();
        apurado.TpPerfil.Should().Be(PerfisUsuarioClinica.Veterinario);

        var reativacao = await client.PostAsJsonAsync($"{Rota}/{criado.Id}/reativacao", new { });

        reativacao.StatusCode.Should().Be(HttpStatusCode.OK);
        var reativado = await reativacao.Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();
        reativado!.StAtiva.Should().BeTrue();

        // E o PUT volta a funcionar - a porta deixou de ser de mao unica.
        (await client.PutAsJsonAsync($"{Rota}/{criado.Id}", new
        {
            dsEmail = email,
            tpPerfil = PerfisUsuarioClinica.Gestor,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reativar_usuario_de_outra_clinica_devolve_404()
    {
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync($"{Rota}/2/reativacao", new { });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_de_GESTOR_sem_clinicaId_degrada_fechado_em_401()
    {
        // A-5: a politica SomenteGestor exige PAPEL e nao exige TENANT - ela APROVA este
        // token. Quem barra e ClinicaContext.IdClinica, que lanca UnauthorizedAccessException
        // quando a claim falta, e o ExceptionHandlerMiddleware mapeia para 401.
        //
        // O registro existe porque a lacuna e real: a policy sozinha nao garante tenant. O que
        // esta medido aqui e que ela degrada FECHADO - nao que ela cubra o caso.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenGestorSemClinicaId());

        var lista = await client.GetAsync(Rota);
        var criacao = await client.PostAsJsonAsync(Rota, CorpoDeCriacao(EmailUnico("sem-tenant")));

        lista.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        criacao.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Decisão de produto: proteção do último gestor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Desativar_o_ultimo_gestor_da_clinica_devolve_422()
    {
        // 🔴 A decisão de produto da FD-04, sobre HTTP e incluindo o caso AUTO-desativação:
        // o gestor autenticado desativa o OUTRO gestor da clínica (204) e depois tenta
        // desativar a SI MESMO — o que deixaria a clínica sem administrador e é recusado.
        //
        // Fábrica PRÓPRIA, descartada no fim: este teste depende da CONTAGEM de gestores da
        // clínica semeada (2 ativos: id 3 = gestor-puro, id 4 = e-mail ambíguo) e desativa os
        // dois. Rodá-lo no banco compartilhado da classe derrubaria o login de GESTOR dos
        // outros testes, conforme a ordem de execução — que o xUnit não garante.
        using var fabrica = new KuraApiFactory();
        var client = fabrica.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));

        // Sobram 2 gestores ativos: desativar um é permitido.
        var primeiro = await client.DeleteAsync($"{Rota}/4");
        primeiro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Agora só resta ele mesmo. Auto-desativação do ÚLTIMO gestor: recusada.
        var ultimo = await client.DeleteAsync($"{Rota}/3");

        ultimo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ultimo.Content.ReadAsStringAsync())
            .Should().Contain("sem nenhum gestor ativo");

        // E ele continua ativo e administrando — a recusa não deixou estado meio aplicado.
        (await client.GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rebaixar_o_ultimo_gestor_da_clinica_devolve_422()
    {
        // Mesma decisão pelo outro caminho: rebaixar a si mesmo para VETERINARIO quando se é
        // o último GESTOR ativo. Fábrica própria, pelo mesmo motivo do teste acima.
        using var fabrica = new KuraApiFactory();
        var client = fabrica.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));

        (await client.DeleteAsync($"{Rota}/4")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resposta = await client.PutAsJsonAsync($"{Rota}/3", new
        {
            dsEmail = KuraApiFactory.EmailGestorPuro,
            tpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rebaixar_gestor_quando_ha_outro_gestor_ativo_devolve_200()
    {
        // 🔴 Controle positivo dos dois testes acima, e a metade PERMISSIVA da decisão: um
        // gestor PODE se rebaixar — o que é recusado é zerar o quadro. Sem este teste, um
        // service que recusasse toda mudança de perfil passaria nos dois anteriores.
        using var fabrica = new KuraApiFactory();
        var client = fabrica.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));

        // Com o id 4 ainda ativo, a clínica tem 2 gestores.
        var resposta = await client.PutAsJsonAsync($"{Rota}/3", new
        {
            dsEmail = KuraApiFactory.EmailGestorPuro,
            tpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await resposta.Content.ReadFromJsonAsync<UsuarioClinicaResponseDto>();
        atualizado!.TpPerfil.Should().Be(PerfisUsuarioClinica.Veterinario);
    }
}
