namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.ServicoPreco;
using Kura.Domain.Entities;

/// <summary>
/// FD-09 — CRUD da tabela de preços exercitado por <b>ROTA HTTP</b>, com o
/// <c>Program.cs</c> real de pé.
///
/// <para>
/// 🔴 <b>Por que teste de rota, e não só de service.</b> O G0 desta task achou que o molde
/// <c>Prescricao</c> não tem teste de controller — só service e validator. Metade do que a
/// FD-09 entrega vive justamente no que um teste de service não alcança: o atributo
/// <c>[Authorize(Policy = SomenteGestor)]</c>, o roteamento, o binder que descarta um
/// <c>idClinica</c> mandado no corpo, o auto-validation do FluentValidation que transforma
/// preço negativo em <c>400</c>, e o <c>ExceptionHandlerMiddleware</c> que transforma
/// <c>EntidadeNaoEncontradaException</c> em <c>404</c>. Um service verde com o controller
/// desprotegido é exatamente o defeito que este arquivo existe para pegar.
/// </para>
///
/// <para>
/// ⚠️ <b>O que esta suíte NÃO prova.</b> O provider é InMemory: ele não aplica
/// <c>CHECK</c>, não aplica <c>UNIQUE</c> e <b>não reprova precisão decimal</b>. O teste de
/// round-trip de <c>10,55</c> aqui vale como contrato do DTO e da serialização JSON —
/// <b>não</b> como prova de banco. A prova contra Oracle real é a FD-12.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class ServicosPrecoHttpTests : IClassFixture<KuraApiFactory>
{
    private const string Rota = "/api/v1/servicos-preco";

    private readonly KuraApiFactory _factory;

    public ServicosPrecoHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteGestorAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));
        return client;
    }

    private static object Corpo(string nome, decimal preco = 100.00m) => new
    {
        nmServico = nome,
        vlPreco = preco,
    };

    private static string NomeUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}";

    // ─────────────────────────────────────────────────────────────────────────────────────
    // A política SomenteGestor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_token_devolve_401()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync(Rota);

        // 401, e não 403: sem credencial nenhuma o pipeline DESAFIA a autenticação antes de
        // chegar à autorização. Um 403 aqui indicaria que o endpoint aceitou o anônimo como
        // autenticado.
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_de_perfil_VETERINARIO_le_o_catalogo_com_200()
    {
        // 🔴 FD-15 (ruling D-13) — MUDANÇA DELIBERADA DE CONTRATO. Antes desta task este
        // mesmo cenário devolvia 403: o controller inteiro exigia SomenteGestor, e o
        // veterinário podia LANÇAR cobrança com idServicoPreco (CobrancasController,
        // [Authorize] simples) sem conseguir LER a tabela para descobrir qual id mandar. A
        // tabela de preços é catálogo operacional — qualquer autenticado da clínica lê; só
        // o GESTOR decide preço (ver o doc-comment de ServicosPrecoController).
        // O usuário semeado em EmailClinica tem TpPerfil = VETERINARIO (KuraApiFactory).
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var resposta = await client.GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await resposta.Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();
        lista.Should().NotBeNull();
        // Escopo continua valendo para leitura de não-gestor: só a própria clínica.
        lista!.Should().OnlyContain(s => s.IdClinica == KuraApiFactory.IdClinicaSemeada);
    }

    [Fact]
    public async Task Token_de_perfil_GESTOR_cria_e_lista()
    {
        // 🔴 A prova de mordida nomeada no backlog, e o controle positivo dos dois testes
        // acima: sem ele, um controller que negasse TODO mundo (política quebrada, rota
        // errada, service não registrado no DI) passaria nos dois anteriores.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("banho-e-tosa");

        var criacao = await client.PostAsJsonAsync(Rota, Corpo(nome, 75.00m));

        criacao.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await criacao.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        criado!.NmServico.Should().Be(nome);
        criado.VlPreco.Should().Be(75.00m);
        criado.StAtiva.Should().BeTrue();

        var listagem = await client.GetAsync(Rota);

        listagem.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await listagem.Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();
        lista.Should().NotBeNull();
        lista!.Should().Contain(s => s.NmServico == nome);
        // Escopo: o outro tenant TEM serviço semeado (KuraApiFactory), então esta asserção é
        // capaz de falhar — ela não é vácua.
        lista.Should().OnlyContain(s => s.IdClinica == KuraApiFactory.IdClinicaSemeada);
        lista.Should().NotContain(s => s.Id == KuraApiFactory.IdServicoPrecoOutroTenant);
    }

    [Fact]
    public async Task Token_pre_FD03_sem_a_claim_perfil_e_barrado_na_ESCRITA_com_403()
    {
        // 🔴 A POLÍTICA TEM DE FALHAR FECHADA — E DEPOIS DA FD-15 ISSO SÓ É MEDÍVEL NUM
        // VERBO DE ESCRITA. Token assinado com a chave real, dentro da validade, no formato
        // que AuthService emitia ANTES da FD-03 — sem a claim `perfil`. Tokens desse formato
        // continuam sendo aceitos pela AUTENTICAÇÃO até expirar; o que decide aqui é a
        // AUTORIZAÇÃO tratar papel AUSENTE como negação (RequireClaim).
        //
        // ⚠️ Antes da FD-15 este teste usava o GET (a política protegia o controller
        // inteiro). Depois da FD-15 o GET só exige [Authorize] simples — não consulta a
        // claim `perfil` — e este MESMO token passaria a ler com 200 (ver o teste seguinte).
        // O verbo que continua exercitando SomenteGestor é a ESCRITA.
        //
        // Se a política fosse escrita como lista de negação ("não é VETERINARIO, logo é
        // GESTOR"), este mesmo token receberia 201 e criaria um serviço na tabela de preços.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("pre-fd03")));

        // 403 e não 401: o token É válido e o usuário ESTÁ autenticado. O que falta é papel.
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_pre_FD03_sem_a_claim_perfil_le_o_catalogo_com_200()
    {
        // 🔴 FD-15 — controle positivo do teste acima, e o que prova que o 403 de lá vem da
        // política de ESCRITA, não de o token estar quebrado de algum outro jeito. O MESMO
        // token, no GET, lê normalmente: [Authorize] simples não exige a claim `perfil`.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_pre_FD03_e_aceito_pela_AUTENTICACAO_em_endpoint_sem_a_politica()
    {
        // 🔴 Controle positivo do teste acima, e o que torna aquele 403 interpretável: o MESMO
        // token forjado devolve 200 num endpoint autenticado SEM a política. Ou seja, o 403 vem
        // da política — não de assinatura inválida, emissor errado ou token expirado, que
        // dariam 401 e passariam despercebidos numa asserção de "não é 200".
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_de_VETERINARIO_le_normalmente_os_DOIS_GETs()
    {
        // 🔴 FD-15 (ruling D-13) — MUDANÇA DELIBERADA DE CONTRATO, metade de leitura do
        // teste que antes desta task se chamava
        // Token_de_VETERINARIO_e_barrado_em_TODOS_os_verbos_do_controller e esperava 403
        // nos dois GETs abaixo. Catálogo operacional: qualquer autenticado da clínica lê.
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var respostas = new[]
        {
            await client.GetAsync(Rota),
            await client.GetAsync($"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}"),
        };

        respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_de_VETERINARIO_e_barrado_em_TODOS_os_verbos_de_ESCRITA()
    {
        // A política SomenteGestor está em CADA MÉTODO de escrita, não no controller (FD-15
        // moveu ela para lá — ver o doc-comment de ServicosPrecoController). Este teste
        // trava essa decisão do lado que ainda é fechado: esquecer o atributo num método de
        // escrita novo deixaria esse endpoint respondendo normalmente para um veterinário.
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var respostas = new[]
        {
            await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("vet-tentou"))),
            await client.PutAsJsonAsync(
                $"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}", Corpo("Remarcado", 1m)),
            await client.PostAsJsonAsync(
                $"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}/reativacao", new { }),
            await client.DeleteAsync($"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}"),
        };

        respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Escopo de escrita e IDOR
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_ignora_idClinica_do_corpo_e_usa_a_do_jwt()
    {
        // 🔴 A prova de mordida da FD-05 aplicada aqui: o corpo carrega idClinica do OUTRO
        // tenant. Como ServicoPrecoCreateDto não tem esse campo, o binder o descarta e a
        // clínica vem do JWT. O campo é mandado DE PROPÓSITO — um DTO que ganhasse IdClinica
        // no futuro faria este teste começar a gravar no tenant errado e falhar.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("corpo-com-clinica");

        var resposta = await client.PostAsJsonAsync(Rota, new
        {
            nmServico = nome,
            vlPreco = 42.00m,
            idClinica = KuraApiFactory.IdClinicaOutroTenant,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await resposta.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        criado!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        criado.IdClinica.Should().NotBe(KuraApiFactory.IdClinicaOutroTenant);

        // Confirmação por leitura, não só pelo eco da criação: a linha é alcançável pelo token
        // da clínica do JWT — logo ela está mesmo neste tenant.
        (await client.GetAsync($"{Rota}/{criado.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ler_servico_de_outra_clinica_devolve_404_com_controle_positivo()
    {
        // IDOR pela leitura. 404, e não 403: a resposta é indistinguível de "não existe",
        // então o endpoint não vira oráculo de existência de id alheio.
        var client = await ClienteGestorAsync();

        var alheio = await client.GetAsync($"{Rota}/{KuraApiFactory.IdServicoPrecoOutroTenant}");

        alheio.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 🔴 CONTROLE POSITIVO: o MESMO verbo, num id da PRÓPRIA clínica, devolve 200. Sem
        // ele, um endpoint quebrado que devolvesse 404 para todo mundo passaria igual.
        var proprio = await client.GetAsync($"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}");
        proprio.StatusCode.Should().Be(HttpStatusCode.OK);
        var lido = await proprio.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        lido!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
    }

    [Fact]
    public async Task Atualizar_servico_de_outra_clinica_devolve_404_com_controle_positivo()
    {
        var client = await ClienteGestorAsync();

        var alheio = await client.PutAsJsonAsync(
            $"{Rota}/{KuraApiFactory.IdServicoPrecoOutroTenant}",
            Corpo("Invadido", 1.00m));

        alheio.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 🔴 CONTROLE POSITIVO: o MESMO verbo, num id próprio, devolve 200 — e o nome do
        // serviço alheio continua intacto (a recusa não gravou nada em lugar nenhum).
        var proprio = await client.PutAsJsonAsync(
            $"{Rota}/{KuraApiFactory.IdServicoPrecoSemeado}",
            Corpo(KuraApiFactory.NomeServicoPrecoSemeado,
                  KuraApiFactory.PrecoServicoPrecoSemeado));
        proprio.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Desativar_servico_de_outra_clinica_devolve_404_com_controle_positivo()
    {
        var client = await ClienteGestorAsync();

        var alheio = await client.DeleteAsync(
            $"{Rota}/{KuraApiFactory.IdServicoPrecoOutroTenant}");

        alheio.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 🔴 CONTROLE POSITIVO: cria um serviço PRÓPRIO e desativa pelo mesmo verbo → 204.
        // Usa um item criado agora, e não o semeado, para não derrubar o item de que os
        // outros testes desta classe dependem (o xUnit não garante ordem de execução).
        var criado = await (await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("descartavel"))))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        (await client.DeleteAsync($"{Rota}/{criado!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reativar_servico_de_outra_clinica_devolve_404()
    {
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(
            $"{Rota}/{KuraApiFactory.IdServicoPrecoOutroTenant}/reativacao", new { });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Contrato do preço
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preco_negativo_devolve_400_tratado_e_nao_500()
    {
        // 🔴 O Oracle tem CHK_SERVICO_PRECO_VALOR CHECK (VL_PRECO >= 0) e o .NET não tinha
        // NADA equivalente (achado da G2 da FD-08). Sem o validator, este corpo atravessaria
        // tudo e morreria no INSERT como ORA-02290 → 500. E a suíte NÃO pegaria: o provider
        // InMemory não aplica CHECK constraint, então a linha negativa ficaria gravada e o
        // teste passaria VERDE. Aqui quem recusa é o validator, na borda.
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("negativo"), -1.00m));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Instrumento com controle: o corpo é um ProblemDetails de VALIDAÇÃO, apontando o
        // campo VlPreco e a mensagem do validator — não um 400 genérico de JSON malformado,
        // nem um 500 disfarçado. (A chave vem em PascalCase: é o nome da PROPRIEDADE do DTO
        // que o ValidationProblemDetails usa, não o nome serializado em camelCase. Medido.)
        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("VlPreco");
        corpo.Should().Contain("validation errors");
    }

    [Fact]
    public async Task Preco_zero_e_aceito_com_201()
    {
        // 🔴 CONTROLE POSITIVO do teste acima, e ele é preciso: o CHECK do Oracle é
        // `>= 0`, não `> 0`. Um validator escrito como GreaterThan(0) recusaria serviço
        // gratuito (retorno de consulta, cortesia) e passaria despercebido sem esta medição.
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("cortesia"), 0m));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await resposta.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        criado!.VlPreco.Should().Be(0m);
    }

    [Fact]
    public async Task Atualizar_com_preco_negativo_tambem_devolve_400()
    {
        // O PUT é a segunda porta pela qual um preço já cadastrado viraria negativo. Um
        // validator de update mais frouxo que o de create deixaria entrar por aqui o que o
        // outro fecha.
        var client = await ClienteGestorAsync();
        var criado = await (await client.PostAsJsonAsync(
                Rota, Corpo(NomeUnico("put-negativo"), 10.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        var resposta = await client.PutAsJsonAsync(
            $"{Rota}/{criado!.Id}", Corpo(criado.NmServico, -0.01m));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 🔴 CONTROLE POSITIVO: o MESMO verbo, com preço válido, devolve 200 — e o preço
        // antigo continua lá, ou seja, a recusa não gravou pela metade.
        var apurado = await (await client.GetAsync($"{Rota}/{criado.Id}"))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        apurado!.VlPreco.Should().Be(10.00m);

        (await client.PutAsJsonAsync($"{Rota}/{criado.Id}", Corpo(criado.NmServico, 12.00m)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Criar_com_10_55_devolve_10_55_na_leitura()
    {
        // ⚠️ CONTRATO DE DTO E SERIALIZAÇÃO, NÃO PROVA DE BANCO. O provider desta suíte é
        // InMemory: ele NÃO reprova precisão — a suíte fica verde com `double` e com
        // HasPrecision errado. O que este teste pega é o outro modo de falha, que é real e
        // mora fora do banco: um DTO declarado como `double` faria 10,55 voltar como
        // 10.550000000000001 pelo System.Text.Json. A prova contra Oracle é a FD-12.
        var client = await ClienteGestorAsync();

        var criacao = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("centavos"), 10.55m));
        criacao.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await criacao.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        criado!.VlPreco.Should().Be(10.55m);

        var leitura = await client.GetAsync($"{Rota}/{criado.Id}");
        var lido = await leitura.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        lido!.VlPreco.Should().Be(10.55m);

        // O JSON cru também: 10.55 e nada de sufixo de ponto flutuante binário.
        var json = await (await client.GetAsync($"{Rota}/{criado.Id}")).Content.ReadAsStringAsync();
        json.Should().Contain("\"vlPreco\":10.55");
    }

    [Fact]
    public async Task Criar_com_mais_de_duas_casas_decimais_devolve_400()
    {
        // NUMBER(10,2) arredonda em SILÊNCIO o que não cabe na escala (medido na FD-07:
        // 999.99 vira 1000 quando a escala some). Recusar na borda é a única forma de o
        // número que entrou ser o número gravado.
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("tres-casas"), 10.555m));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Decisão de produto: nome duplicado × soft delete (item 3 do brief)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_com_nome_de_servico_ATIVO_devolve_422()
    {
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("duplicado");

        (await client.PostAsJsonAsync(Rota, Corpo(nome)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await client.PostAsJsonAsync(Rota, Corpo(nome));

        // 422 do ExceptionHandlerMiddleware (RegraDeNegocioException) — erro de negócio
        // tratado. ⚠️ O banco NÃO pegaria isto: a FD-07 deliberadamente não criou
        // UNIQUE (ID_CLINICA, NM_SERVICO), e o InMemory não aplica índice único de qualquer
        // forma. A recusa é 100% código desta task.
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("RegraDeNegocioException");
    }

    [Fact]
    public async Task Nome_de_servico_DESATIVADO_pode_ser_recadastrado()
    {
        // 🔴 A DECISÃO DE PRODUTO DESTA TASK, e o teste que impede a regressão para o defeito
        // A-3 da FD-04 (o e-mail que ficava reservado para sempre). A FD-07 deliberadamente
        // NÃO criou UNIQUE (ID_CLINICA, NM_SERVICO) justamente para permitir isto; uma
        // checagem de nome duplicado que olhasse TODAS as linhas seria aquela unique
        // reescrita em código, e queimaria o nome do serviço para sempre.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("volta-do-mesmo-nome");

        var primeiro = await (await client.PostAsJsonAsync(Rota, Corpo(nome, 50.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        (await client.DeleteAsync($"{Rota}/{primeiro!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O MESMO nome, agora que o primeiro está desativado: aceito.
        var recadastro = await client.PostAsJsonAsync(Rota, Corpo(nome, 60.00m));

        recadastro.StatusCode.Should().Be(HttpStatusCode.Created);
        var segundo = await recadastro.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        segundo!.Id.Should().NotBe(primeiro.Id);
        segundo.VlPreco.Should().Be(60.00m);

        // E o desativado sumiu da lista, sem levar o novo junto.
        var lista = await (await client.GetAsync(Rota))
            .Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();
        lista!.Should().Contain(s => s.Id == segundo.Id);
        lista.Should().NotContain(s => s.Id == primeiro.Id);
    }

    [Fact]
    public async Task Reativar_quando_o_nome_foi_recadastrado_devolve_422_e_o_caminho_de_volta_existe()
    {
        // O contrapeso da decisão: recadastrar é permitido, mas reativar o antigo depois disso
        // deixaria DOIS ativos com o mesmo nome — o estado que a criação recusa. 422, e a
        // saída dentro do produto é renomear o outro. Sem esta última metade, a recusa seria
        // porta de mão única e a decisão viraria o defeito que ela evita.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("colisao-na-volta");

        var antigo = await (await client.PostAsJsonAsync(Rota, Corpo(nome, 50.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        await client.DeleteAsync($"{Rota}/{antigo!.Id}");

        var novo = await (await client.PostAsJsonAsync(Rota, Corpo(nome, 60.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        var colisao = await client.PostAsJsonAsync($"{Rota}/{antigo.Id}/reativacao", new { });
        colisao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 🔴 CONTROLE POSITIVO / caminho de volta: renomeia o novo e a reativação passa.
        (await client.PutAsJsonAsync($"{Rota}/{novo!.Id}", Corpo(NomeUnico("renomeado"), 60.00m)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reativacao = await client.PostAsJsonAsync($"{Rota}/{antigo.Id}/reativacao", new { });
        reativacao.StatusCode.Should().Be(HttpStatusCode.OK);
        var reativado = await reativacao.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        reativado!.StAtiva.Should().BeTrue();
        reativado.Id.Should().Be(antigo.Id);
    }

    [Fact]
    public async Task Servico_desativado_recusa_PUT_com_422()
    {
        // A-3 da FD-04 sobre HTTP: antes daquela fix wave, o PUT sobre item desativado
        // respondia 200 sem reativar — sucesso silencioso, a classe de defeito da TASK-69.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("put-em-desativado");

        var criado = await (await client.PostAsJsonAsync(Rota, Corpo(nome, 30.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        await client.DeleteAsync($"{Rota}/{criado!.Id}");

        var put = await client.PutAsJsonAsync($"{Rota}/{criado.Id}", Corpo(nome, 31.00m));

        put.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await put.Content.ReadAsStringAsync()).Should().Contain("DESATIVADO");

        // A gravação não pode ter acontecido junto com a recusa.
        var apurado = await (await client.GetAsync($"{Rota}/{criado.Id}"))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        apurado!.VlPreco.Should().Be(30.00m);
        apurado.StAtiva.Should().BeFalse();
    }

    [Fact]
    public async Task Criar_com_nome_vazio_devolve_400_de_contrato()
    {
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(Rota, Corpo("   ", 10.00m));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_de_GESTOR_sem_clinicaId_degrada_fechado_em_401()
    {
        // A política SomenteGestor exige PAPEL e não exige TENANT — ela APROVA este token.
        // Quem barra é ClinicaContext.IdClinica, que lança UnauthorizedAccessException quando
        // a claim falta, e o ExceptionHandlerMiddleware mapeia para 401. O registro existe
        // porque a lacuna é real: o que está medido aqui é que ela degrada FECHADO.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenGestorSemClinicaId());

        var lista = await client.GetAsync(Rota);
        var criacao = await client.PostAsJsonAsync(Rota, Corpo(NomeUnico("sem-tenant")));

        lista.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        criacao.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // FD-16 — ?incluirInativos
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_sem_incluirInativos_continua_omitindo_o_desativado()
    {
        // Controle de regressão: o comportamento SEM o parâmetro tem de continuar
        // BYTE A BYTE o de antes da FD-16 — nada que já consome a rota pode mudar.
        var client = await ClienteGestorAsync();
        var nome = NomeUnico("fd16-sem-flag");
        var criado = await (await client.PostAsJsonAsync(Rota, Corpo(nome, 20.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        (await client.DeleteAsync($"{Rota}/{criado!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var lista = await (await client.GetAsync(Rota))
            .Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();

        lista!.Should().NotContain(s => s.Id == criado.Id);
    }

    [Fact]
    public async Task Listar_com_incluirInativos_true_traz_o_desativado_e_mantem_o_ativo()
    {
        var client = await ClienteGestorAsync();
        var nomeInativo = NomeUnico("fd16-inativo");
        var nomeAtivo = NomeUnico("fd16-ativo");

        var inativo = await (await client.PostAsJsonAsync(Rota, Corpo(nomeInativo, 21.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();
        (await client.DeleteAsync($"{Rota}/{inativo!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ativo = await (await client.PostAsJsonAsync(Rota, Corpo(nomeAtivo, 22.00m)))
            .Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        var lista = await (await client.GetAsync($"{Rota}?incluirInativos=true"))
            .Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();

        lista!.Should().Contain(s => s.Id == inativo.Id && !s.StAtiva);
        // 🔴 Controle positivo: o flag ACRESCENTA, não SUBSTITUI — o ativo continua na
        // lista. Sem esta asserção, um bug que trocasse StAtiva por !StAtiva no predicado
        // passaria despercebido.
        lista.Should().Contain(s => s.Id == ativo!.Id && s.StAtiva);
    }

    [Fact]
    public async Task Listar_com_incluirInativos_true_NAO_vaza_de_outra_clinica()
    {
        // 🔴 R-1 da revisão G2 da FD-16 — a prova HTTP que faltava, e o diagnóstico que a
        // destravou. O implementador declarou (honestamente) que provar isto exigiria semear
        // um serviço INATIVO do tenant 2 em KuraApiFactory, e não quis mexer no fixture
        // compartilhado. Não exige: a mutação que importa é
        // `incluirInativos || (IdClinica == idClinica && StAtiva)`, que com o flag LIGADO
        // devolve TODA linha da tabela — inclusive as ATIVAS do tenant 2, já semeadas há
        // três tasks como isca de IDOR (IdServicoPrecoOutroTenant). O vazamento aparece sem
        // nenhum dado novo.
        var client = await ClienteGestorAsync();

        var lista = await (await client.GetAsync($"{Rota}?incluirInativos=true"))
            .Content.ReadFromJsonAsync<List<ServicoPrecoResponseDto>>();

        // Controle positivo: sem esta linha o OnlyContain abaixo passa POR VÁCUO numa lista
        // vazia — e um `0` só é interpretável se o instrumento enxergaria um `1`.
        lista.Should().NotBeNull();
        lista!.Should().NotBeEmpty();
        lista.Should().Contain(s => s.Id == KuraApiFactory.IdServicoPrecoSemeado);

        lista.Should().OnlyContain(s => s.IdClinica == KuraApiFactory.IdClinicaSemeada);
    }
}
