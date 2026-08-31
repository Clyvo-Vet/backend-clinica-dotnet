namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Cobranca;
using Kura.Application.DTOs.ServicoPreco;
using Kura.Application.Validators;

/// <summary>
/// FD-10 — lançamento de cobrança exercitado por <b>ROTA HTTP</b>, com o <c>Program.cs</c>
/// real de pé.
///
/// <para>
/// 🔴 <b>Por que teste de rota, e não só de service.</b> A metade da task que um teste de
/// service não alcança é justamente a que a ruling de autorização decide: quem consegue
/// <c>POST</c> e quem consegue <c>GET</c>. Um service verde com o <c>POST</c> fechado para o
/// veterinário seria a inversão exata do princípio de desenho do ciclo — o dado financeiro
/// passaria a depender de o gestor redigitar tudo depois — e nenhum teste de service veria
/// isso. Aqui também moram o roteamento do subrecurso, o auto-validation do FluentValidation
/// (<c>400</c>) e o <c>ExceptionHandlerMiddleware</c> (<c>404</c>/<c>422</c>).
/// </para>
///
/// <para>
/// ⚠️ <b>O que esta suíte NÃO prova.</b> O provider é InMemory: ele não aplica <c>CHECK</c>,
/// não aplica <c>FOREIGN KEY</c> e não reprova precisão decimal. O <c>400</c> do valor
/// negativo aqui vale como prova do <b>validator</b> — a prova de que o banco também
/// recusaria é a FD-12, contra Oracle real.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class CobrancasHttpTests : IClassFixture<KuraApiFactory>
{
    private const string RotaServicosPreco = "/api/v1/servicos-preco";

    private readonly KuraApiFactory _factory;

    public CobrancasHttpTests(KuraApiFactory factory) => _factory = factory;

    private static string Rota(long idEvento) =>
        $"/api/v1/eventos-clinicos/{idEvento}/cobrancas";

    /// <summary>Token do usuário <c>VETERINARIO</c> semeado — é ele quem fecha o atendimento.</summary>
    private async Task<HttpClient> ClienteVeterinarioAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    private async Task<HttpClient> ClienteGestorAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));
        return client;
    }

    private static string NomeUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}";

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 O princípio de desenho: o VETERINÁRIO lança, no fechamento do atendimento
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Veterinario_CONSEGUE_lancar_cobranca()
    {
        // 🔴 A prova de mordida do princípio de desenho do ciclo: "o dado do gestor nasce
        // como subproduto do fluxo do veterinário". EventosClinicosController é [Authorize]
        // simples — quem cria evento clínico é o veterinário. Se o POST daqui exigisse
        // SomenteGestor, o veterinário levaria 403 no fechamento e o financeiro passaria a
        // depender de redigitação. Este teste é o que trava a ruling contra regressão.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { vlCobrado = 90.00m, dsFormaPagamento = "PIX" });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var criada = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        criada!.VlCobrado.Should().Be(90.00m);
        criada.IdEventoClinico.Should().Be(KuraApiFactory.IdEventoClinicoSemeado);
        criada.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        criada.DsFormaPagamento.Should().Be("PIX");
    }

    [Fact]
    public async Task Veterinario_lanca_com_o_corpo_MINIMO_so_o_servico_de_preco()
    {
        // O gesto de fechamento que a task desenha: um toque na tabela de preços, sem
        // digitar valor nenhum no meio do atendimento.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { idServicoPreco = KuraApiFactory.IdServicoPrecoSemeado });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var criada = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        criada!.VlCobrado.Should().Be(KuraApiFactory.PrecoServicoPrecoSemeado);
        criada.IdServicoPreco.Should().Be(KuraApiFactory.IdServicoPrecoSemeado);
    }

    [Fact]
    public async Task Gestor_TAMBEM_consegue_lancar()
    {
        // Controle positivo do lado oposto: [Authorize] simples não vira "só veterinário".
        var client = await ClienteGestorAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 15.00m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Sem_token_o_lancamento_devolve_401()
    {
        var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 10.00m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 D-7: a LEITURA do financeiro é do gestor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Veterinario_e_barrado_em_TODOS_os_GET_de_cobranca()
    {
        // D-7: o financeiro é VISÍVEL só para o gestor. Escrita e leitura têm autorizações
        // diferentes de propósito neste controller — ver o cabeçalho de CobrancasController.
        var client = await ClienteVeterinarioAsync();

        var respostas = new[]
        {
            await client.GetAsync(Rota(KuraApiFactory.IdEventoClinicoSemeado)),
            await client.GetAsync($"{Rota(KuraApiFactory.IdEventoClinicoSemeado)}/1"),
        };

        respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gestor_LE_o_que_o_veterinario_lancou()
    {
        // 🔴 CONTROLE POSITIVO do teste acima, e a demonstração do fluxo inteiro numa
        // requisição só: o veterinário lança no fechamento, o gestor vê. Sem este teste, um
        // controller que negasse TODO GET (política quebrada, rota errada, service fora do
        // DI) passaria naquele 403.
        var veterinario = await ClienteVeterinarioAsync();
        var gestor = await ClienteGestorAsync();

        var lancamento = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 61.25m });
        lancamento.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await lancamento.Content.ReadFromJsonAsync<CobrancaResponseDto>();

        var leitura = await gestor.GetAsync(
            $"{Rota(KuraApiFactory.IdEventoClinicoSemeado)}/{criada!.Id}");

        leitura.StatusCode.Should().Be(HttpStatusCode.OK);
        var lida = await leitura.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        lida!.VlCobrado.Should().Be(61.25m);
        lida.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);

        var listagem = await gestor.GetAsync(Rota(KuraApiFactory.IdEventoClinicoSemeado));
        listagem.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await listagem.Content.ReadFromJsonAsync<List<CobrancaResponseDto>>();
        lista.Should().NotBeNull();
        lista!.Should().Contain(c => c.Id == criada.Id);
        lista.Should().OnlyContain(c => c.IdClinica == KuraApiFactory.IdClinicaSemeada);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 O INVARIANTE CENTRAL, provado ponta a ponta por HTTP
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remarcar_a_tabela_de_precos_NAO_altera_cobranca_ja_lancada()
    {
        // 🔴 A prova de mordida mais importante da FD-10, aqui atravessando os DOIS
        // controllers reais: FD-09 remarca o preço, FD-10 devolve a cobrança. Se VL_COBRADO
        // fosse resolvido por FK na leitura, o histórico financeiro se reescreveria sozinho
        // a cada correção de preço.
        var gestor = await ClienteGestorAsync();
        var veterinario = await ClienteVeterinarioAsync();

        // 1. Serviço PRÓPRIO deste teste (não o semeado): remarcar o compartilhado
        //    contaminaria os outros testes desta classe, que dividem o mesmo banco InMemory.
        var criacaoServico = await gestor.PostAsJsonAsync(RotaServicosPreco, new
        {
            nmServico = NomeUnico("copia-de-valor"),
            vlPreco = 200.00m,
        });
        criacaoServico.StatusCode.Should().Be(HttpStatusCode.Created);
        var servico = await criacaoServico.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        // 2. O veterinário lança a cobrança pelo serviço, sem digitar valor.
        var lancamento = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { idServicoPreco = servico!.Id });
        lancamento.StatusCode.Should().Be(HttpStatusCode.Created);
        var cobranca = await lancamento.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        cobranca!.VlCobrado.Should().Be(200.00m);

        // 3. O gestor remarca a tabela de preços — o dobro, para que qualquer leitura por FK
        //    seja inconfundível.
        var remarcacao = await gestor.PutAsJsonAsync($"{RotaServicosPreco}/{servico.Id}", new
        {
            nmServico = servico.NmServico,
            vlPreco = 400.00m,
        });
        remarcacao.StatusCode.Should().Be(HttpStatusCode.OK);

        // 🔴 CONTROLE POSITIVO DO INSTRUMENTO: o preço REALMENTE mudou. Sem esta leitura,
        // um PUT que não tivesse efeito faria o teste provar "o valor não mudou" num cenário
        // onde nada mudou — verde por vácuo.
        var precoAtual = await gestor.GetFromJsonAsync<ServicoPrecoResponseDto>(
            $"{RotaServicosPreco}/{servico.Id}");
        precoAtual!.VlPreco.Should().Be(400.00m);

        // 4. A cobrança relida continua valendo o preço do INSTANTE DO LANÇAMENTO.
        var relida = await gestor.GetFromJsonAsync<CobrancaResponseDto>(
            $"{Rota(KuraApiFactory.IdEventoClinicoSemeado)}/{cobranca.Id}");

        relida!.VlCobrado.Should().Be(200.00m);
        relida.VlCobrado.Should().NotBe(400.00m);
        // A ORIGEM continua rastreável — o que não vira fonte de valor.
        relida.IdServicoPreco.Should().Be(servico.Id);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 Travas de tenant, com a isca EXISTINDO no banco
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lancar_em_evento_de_OUTRA_clinica_devolve_404()
    {
        // 🔴 A isca EXISTE (KuraApiFactory semeia IdEventoClinicoOutroTenant). Sem ela este
        // 404 viria de "não existe para ninguém" e não provaria escopo nenhum — foi a lição
        // da FD-09.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoOutroTenant), new { vlCobrado = 10.00m });

        // 404 e não 403: id de outra clínica é indistinguível de inexistente de propósito —
        // um 403 confirmaria a existência do id alheio (enumeração de tenant de graça).
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lancar_em_evento_da_PROPRIA_clinica_devolve_201()
    {
        // 🔴 CONTROLE POSITIVO do teste acima: sem ele, um endpoint que devolvesse 404 para
        // TODO evento (rota errada, repositório quebrado, predicado invertido) passaria lá.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 10.00m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Listar_cobrancas_de_evento_de_OUTRA_clinica_devolve_404()
    {
        var gestor = await ClienteGestorAsync();

        var alheio = await gestor.GetAsync(Rota(KuraApiFactory.IdEventoClinicoOutroTenant));
        alheio.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Controle positivo: o próprio evento lista normalmente.
        var proprio = await gestor.GetAsync(Rota(KuraApiFactory.IdEventoClinicoSemeado));
        proprio.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lancar_com_servico_de_preco_de_OUTRA_clinica_devolve_422()
    {
        // A isca também existe aqui: IdServicoPrecoOutroTenant é semeado na clínica 2.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { idServicoPreco = KuraApiFactory.IdServicoPrecoOutroTenant });

        // 422 e não 404: a rota existe e o pedido é bem-formado — o que falha é uma regra
        // sobre uma referência do CORPO. A mensagem é a mesma de "não existe", para não
        // vazar a existência do id alheio.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // E nada do outro tenant vazou como valor: o serviço da clínica 2 custa 999,99.
        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain("999");
    }

    [Fact]
    public async Task Lancar_com_servico_de_preco_da_PROPRIA_clinica_devolve_201()
    {
        // 🔴 CONTROLE POSITIVO do teste acima e do de serviço desativado.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { idServicoPreco = KuraApiFactory.IdServicoPrecoSemeado });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Lancar_com_servico_de_preco_DESATIVADO_devolve_422()
    {
        var gestor = await ClienteGestorAsync();
        var veterinario = await ClienteVeterinarioAsync();

        // Serviço próprio deste teste, criado e depois desativado pelo caminho de produto.
        var criacao = await gestor.PostAsJsonAsync(RotaServicosPreco, new
        {
            nmServico = NomeUnico("desativado"),
            vlPreco = 10.00m,
        });
        var servico = await criacao.Content.ReadFromJsonAsync<ServicoPrecoResponseDto>();

        // Controle positivo: enquanto ATIVO, ele lança normalmente.
        var enquantoAtivo = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { idServicoPreco = servico!.Id });
        enquantoAtivo.StatusCode.Should().Be(HttpStatusCode.Created);

        (await gestor.DeleteAsync($"{RotaServicosPreco}/{servico.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depoisDeDesativado = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { idServicoPreco = servico.Id });

        depoisDeDesativado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Corpo_com_idClinica_e_idEventoClinico_e_ignorado_o_escopo_vem_da_rota_e_do_JWT()
    {
        // O corpo carrega os dois campos DE PROPÓSITO, apontando o outro tenant. Como
        // CobrancaCreateDto não os tem, o binder os descarta: a clínica vem do JWT e o
        // evento vem da ROTA. Um DTO que ganhasse esses campos no futuro faria este teste
        // começar a gravar no tenant errado e falhar.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new
            {
                vlCobrado = 12.00m,
                idClinica = KuraApiFactory.IdClinicaOutroTenant,
                idEventoClinico = KuraApiFactory.IdEventoClinicoOutroTenant,
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        criada!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        criada.IdEventoClinico.Should().Be(KuraApiFactory.IdEventoClinicoSemeado);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 F1 da revisão G2 — o segmento idEventoClinico da rota não é decoração
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Obter_cobranca_por_um_evento_que_NAO_e_o_dela_devolve_404()
    {
        // 🔴 F1, medido na revisão: antes do fix esta requisição devolvia 200. A cobrança é
        // da clínica do token e a leitura é feita pelo gestor dela — o único erro é o
        // segmento do meio da rota. O XML doc do método prometia um 404 que nunca acontecia.
        var veterinario = await ClienteVeterinarioAsync();
        var gestor = await ClienteGestorAsync();

        var lancamento = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 44.00m });
        var criada = await lancamento.Content.ReadFromJsonAsync<CobrancaResponseDto>();

        // Evento de OUTRO tenant no segmento do meio.
        var porEventoAlheio = await gestor.GetAsync(
            $"{Rota(KuraApiFactory.IdEventoClinicoOutroTenant)}/{criada!.Id}");
        porEventoAlheio.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Evento INEXISTENTE no segmento do meio.
        var porEventoInexistente = await gestor.GetAsync($"{Rota(999999)}/{criada.Id}");
        porEventoInexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 🔴 CONTROLE POSITIVO: a MESMA cobrança, pelo evento certo, devolve 200. Sem ele,
        // um endpoint quebrado que devolvesse 404 sempre passaria nos dois acima.
        var pelaRotaCerta = await gestor.GetAsync(
            $"{Rota(KuraApiFactory.IdEventoClinicoSemeado)}/{criada.Id}");
        pelaRotaCerta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Obter_cobranca_do_OUTRO_tenant_por_id_devolve_404()
    {
        // 🔴 F3 da revisão G2: esta é a asserção que ficava VÁCUA antes de KuraApiFactory
        // semear uma COBRANCA na clínica 2 — sem linha alheia no banco, o 404 viria de "não
        // existe para ninguém" e a mutação do predicado de tenant do CobrancaRepository não
        // mordia no HTTP (mordia só nas unitárias).
        //
        // A rota usa o evento DONO da cobrança alheia de propósito: assim o predicado de
        // evento (F1) casa, e a ÚNICA coisa entre a resposta e o vazamento é a comparação
        // de clínica.
        var gestor = await ClienteGestorAsync();

        var resposta = await gestor.GetAsync(
            $"{Rota(KuraApiFactory.IdEventoClinicoOutroTenant)}"
            + $"/{KuraApiFactory.IdCobrancaOutroTenant}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // O valor da isca é 777,77 — se ele aparecer no corpo, vazou.
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("777");
    }

    [Fact]
    public async Task Obter_cobranca_da_PROPRIA_clinica_por_id_devolve_200()
    {
        // 🔴 CONTROLE POSITIVO do teste acima: a mesma forma de leitura, na clínica do
        // token, devolve a linha. Sem ele, um repositório que não achasse nada nunca
        // passaria naquele 404 por mérito.
        var veterinario = await ClienteVeterinarioAsync();
        var gestor = await ClienteGestorAsync();

        var lancamento = await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 55.00m });
        var criada = await lancamento.Content.ReadFromJsonAsync<CobrancaResponseDto>();

        var resposta = await gestor.GetAsync(
            $"{Rota(KuraApiFactory.IdEventoClinicoSemeado)}/{criada!.Id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var lida = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        lida!.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
        lida.VlCobrado.Should().Be(55.00m);
    }

    [Fact]
    public async Task Listagem_do_proprio_evento_NUNCA_traz_a_cobranca_do_outro_tenant()
    {
        // Segundo consumidor da mesma isca (F3): a listagem. A asserção só é capaz de falhar
        // porque a cobrança da clínica 2 EXISTE no banco.
        var veterinario = await ClienteVeterinarioAsync();
        var gestor = await ClienteGestorAsync();

        await veterinario.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 66.00m });

        var lista = await gestor.GetFromJsonAsync<List<CobrancaResponseDto>>(
            Rota(KuraApiFactory.IdEventoClinicoSemeado));

        lista.Should().NotBeNullOrEmpty();
        lista!.Should().OnlyContain(c => c.IdClinica == KuraApiFactory.IdClinicaSemeada);
        lista.Should().NotContain(c => c.VlCobrado == KuraApiFactory.ValorCobrancaOutroTenant);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Contrato de entrada — o que o Oracle recusaria e o InMemory gravaria
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Valor_negativo_devolve_400_TRATADO()
    {
        // 🔴 Sem o validator, isto seria 201 nesta suíte (o InMemory não aplica CHECK) e
        // ORA-02290/500 em produção. O que se assere aqui é o 4xx TRATADO, não "não é 201".
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = -1.00m });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ((int)resposta.StatusCode).Should().BeInRange(400, 499);
    }

    [Fact]
    public async Task Valor_ZERO_devolve_201()
    {
        // Controle positivo do teste acima: a fronteira do CHECK do Oracle é `>= 0`.
        // Cortesia é lançamento legítimo, e um validator que recusasse zero passaria no
        // teste do negativo estando errado.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 0m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Corpo_sem_valor_e_sem_servico_devolve_400()
    {
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { dsFormaPagamento = "PIX" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DtCobranca_0001_01_01_devolve_400()
    {
        // 🔴 O valor que NÃO é nulo, passa pelo NOT NULL do Oracle e some de todo KPI por
        // período da FD-11. Mandado pelo fio, no formato exato que um cliente produziria ao
        // serializar um DateTime não inicializado.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { vlCobrado = 10.00m, dtCobranca = "0001-01-01T00:00:00" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DtCobranca_retroativa_de_ontem_devolve_201_e_preserva_a_data()
    {
        // 🔴 Controle positivo do teste acima — e o caso de uso que justifica aceitar data
        // do cliente: o fechamento do dia anterior lançado na manhã seguinte. Sem ele, um
        // validator que recusasse TODA data informada passaria naquele 400.
        var client = await ClienteVeterinarioAsync();
        var ontem = DateTime.UtcNow.Date.AddDays(-1).AddHours(18);

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado),
            new { vlCobrado = 10.00m, dtCobranca = ontem });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        criada!.DtCobranca.Should().BeCloseTo(ontem, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DtCobranca_ausente_nasce_com_a_hora_do_lancamento_e_nunca_0001()
    {
        var client = await ClienteVeterinarioAsync();
        var antes = DateTime.UtcNow.AddMinutes(-1);

        var resposta = await client.PostAsJsonAsync(
            Rota(KuraApiFactory.IdEventoClinicoSemeado), new { vlCobrado = 10.00m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await resposta.Content.ReadFromJsonAsync<CobrancaResponseDto>();
        criada!.DtCobranca.Should().NotBe(default);
        criada.DtCobranca.Should().BeAfter(CobrancaCreateValidator.DataMinima);
        criada.DtCobranca.Should().BeOnOrAfter(antes);
    }
}
