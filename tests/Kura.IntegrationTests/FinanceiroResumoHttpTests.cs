namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Financeiro;
using Kura.Application.Services;

/// <summary>
/// FD-11 — o resumo financeiro exercitado por <b>ROTA HTTP</b>, com o <c>Program.cs</c> real
/// de pé.
///
/// <para>
/// 🔴 <b>Por que teste de rota, e não só de service.</b> Três metades da task são invisíveis a
/// um teste de service: (1) a <b>política</b> — quem enxerga o financeiro (D-7) é decisão de
/// autorização, e um service verde com o endpoint aberto ao veterinário seria a violação
/// exata da ruling; (2) o <b>model binding</b> de <c>de</c>/<c>ate</c>, onde mora metade dos
/// <c>400</c> (formato inválido morre no binder, ausência morre no validator, e os dois
/// devolvem <c>400</c> por caminhos diferentes); (3) a <b>serialização</b> — um
/// <c>ticketMedio</c> nulo que virasse <c>0</c> no JSON desfaria a ruling inteira sem quebrar
/// nenhum teste de service.
/// </para>
///
/// <para>
/// ⚠️ <b>Cada cenário usa um ANO próprio, e isso não é estilo.</b> Esta classe compartilha um
/// banco InMemory entre seus testes (<c>IClassFixture</c>), e o resumo é agregado: um teste
/// que somasse a cobrança de outro teste passaria ou falharia conforme a <b>ordem de
/// execução</b>, que o xUnit não garante. Anos <b>não adjacentes</b> (2011, 2013, 2015,
/// 2017, 2019) porque o período de comparação é o intervalo imediatamente anterior — com anos
/// vizinhos, a base de comparação de um teste seria o período de outro.
/// </para>
///
/// <para>
/// ⚠️ <b>O que esta suíte NÃO prova.</b> O provider é InMemory: não há <c>CHECK</c>, nem
/// <c>FOREIGN KEY</c>, nem precisão decimal reprovada, nem tradução SQL de faixa de datas
/// contra Oracle. A agregação daqui roda em memória de propósito (ver
/// <see cref="FinanceiroService"/>), mas o <b>filtro</b> de faixa é traduzido pelo provider —
/// e essa tradução só é exercitada contra Oracle de verdade na FD-12.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class FinanceiroResumoHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public FinanceiroResumoHttpTests(KuraApiFactory factory) => _factory = factory;

    private static string Rota(string de, string ate) =>
        $"/api/v1/financeiro/resumo?de={de}&ate={ate}";

    private async Task<HttpClient> ClienteGestorAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(
            await AutenticacaoHelper.ObterTokenAsync(client, KuraApiFactory.EmailGestorPuro));
        return client;
    }

    private async Task<HttpClient> ClienteVeterinarioAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    /// <summary>
    /// Lança uma cobrança pela rota REAL da FD-10, com a data do fato explícita. Usar o
    /// endpoint de produção em vez de escrever no <c>DbContext</c> mantém o teste honesto: o
    /// que ele agrega é o que o produto grava.
    /// </summary>
    private static async Task LancarAsync(
        HttpClient client,
        long idEventoClinico,
        decimal valor,
        string dtCobrancaIso,
        long? idServicoPreco = null)
    {
        var resposta = await client.PostAsJsonAsync(
            $"/api/v1/eventos-clinicos/{idEventoClinico}/cobrancas",
            new { vlCobrado = valor, dtCobranca = dtCobrancaIso, idServicoPreco });

        resposta.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "o arranjo do teste não pode falhar em silêncio: uma cobrança que não foi lançada "
            + "produziria um resumo zerado que passaria por 'não vazou'");
    }

    private static async Task<ResumoFinanceiroResponseDto> LerResumoAsync(HttpResponseMessage r)
    {
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await r.Content.ReadFromJsonAsync<ResumoFinanceiroResponseDto>())!;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 D-7 — o financeiro agregado é VISÍVEL só para o gestor
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Gestor_obtem_o_resumo_com_200()
    {
        // 🔴 O CONTROLE POSITIVO da política. Sem ele, o 403 do veterinário abaixo seria
        // compatível com "o endpoint recusa todo mundo" — ou com o endpoint não existir.
        var client = await ClienteGestorAsync();

        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 120.00m,
            "2011-06-15T10:00:00Z");

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("2011-01-01", "2011-12-31")));

        resumo.ReceitaBruta.Should().Be(120.00m);
        resumo.NrAtendimentosCobrados.Should().Be(1);
        resumo.TicketMedio.Should().Be(120.00m);
        resumo.Periodo.De.Should().Be(new DateOnly(2011, 1, 1));
        resumo.Periodo.Ate.Should().Be(new DateOnly(2011, 12, 31));
    }

    [Fact]
    public async Task Veterinario_e_barrado_com_403()
    {
        // D-7: quem produz o lançamento é o veterinário (FD-10, POST aberto a ele); quem
        // ENXERGA o financeiro é o gestor. Aqui tudo é leitura agregada, então a política
        // está no controller inteiro — diferente de CobrancasController, que é misto.
        var client = await ClienteVeterinarioAsync();

        var resposta = await client.GetAsync(Rota("2011-01-01", "2011-12-31"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sem_token_devolve_401()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync(Rota("2011-01-01", "2011-12-31"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_VALIDO_anterior_a_FD03_sem_a_claim_perfil_devolve_403()
    {
        // A política SomenteGestor é lista de PERMISSÃO: ausência de claim é negação. Tokens
        // desse formato continuam válidos até expirar, então este não é um cenário inventado.
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenPreFd03());

        var resposta = await client.GetAsync(Rota("2011-01-01", "2011-12-31"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-3 — o período é OBRIGATÓRIO, e os 400 vêm por caminhos diferentes
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/financeiro/resumo")]
    [InlineData("/api/v1/financeiro/resumo?de=2011-01-01")]
    [InlineData("/api/v1/financeiro/resumo?ate=2011-12-31")]
    public async Task Periodo_ausente_devolve_400(string rota)
    {
        // Sem default de servidor: um cliente com bug que esquecesse o período receberia 200
        // com números plausíveis de OUTRO período — o formato de defeito que este ciclo
        // persegue. 400 é visível; número plausível não é.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("ontem", "2011-12-31")]
    [InlineData("2011-13-45", "2011-12-31")]
    [InlineData("2011-01-01", "31/12/2011")]
    [InlineData("2011-01-01T10:00:00Z", "2011-12-31")]
    public async Task Data_mal_formada_devolve_400(string de, string ate)
    {
        // Este caminho NÃO passa pelo validator: formato inválido morre no model binding e o
        // [ApiController] traduz em 400. Medido aqui em vez de deduzido — se o binder mudar
        // de comportamento, este teste cai, não a documentação.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota(de, ate));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 F1 da fix wave pós-G2 — NENHUM valor do domínio de `DateOnly` produz 5xx
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("9999-12-01", "9999-12-31")]
    [InlineData("9999-12-31", "9999-12-31")]
    [InlineData("0001-01-01", "0001-01-31")]
    [InlineData("0001-01-01", "0001-01-01")]
    [InlineData("0001-01-01", "9999-12-31")]
    [InlineData("2010-01-01", "2020-01-01")]
    public async Task Periodo_NAO_COMPUTAVEL_devolve_400_e_nunca_5xx(string de, string ate)
    {
        // 🔴 A MORDIDA DO F1. Antes desta fix wave os três primeiros casos devolviam **500**
        // (`ArgumentOutOfRangeException`): `DateOnly.AddDays` LANÇA fora de
        // [0001-01-01, 9999-12-31] em vez de saturar, e o service chamava
        // `ate.AddDays(1)` (borda superior) e `De.AddDays(-duracao)` (período anterior, um
        // cálculo derivado que o gestor nem pediu). `?ate=9999-12-31` não é hipótese de
        // laboratório: é o que um seletor de data com campo vazio ou `max` produz sozinho.
        //
        // O último caso é a guarda de VOLUME (teto de duração), não de calendário — 2010→2020
        // é perfeitamente computável e mesmo assim é recusado, porque a agregação é feita em
        // MEMÓRIA. Os dois motivos são diferentes de propósito e NÃO podem ser colapsados num
        // só: 9999-12-01→9999-12-31 são 31 dias e passam por qualquer teto de duração.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota(de, ate));

        ((int)resposta.StatusCode).Should().BeLessThan(500,
            "nenhum valor dentro do domínio aceito de DateOnly pode produzir 5xx neste endpoint");
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CONTROLE_POSITIVO_ano_alto_porem_computavel_devolve_200()
    {
        // 🔴 Sem este caso, um validator que recusasse TUDO passaria nos 6 casos acima. Ele é
        // o que separa "a borda foi fechada" de "o endpoint inteiro foi fechado". 9998-12-01..12-31 é
        // vizinho imediato da borda que estoura e é perfeitamente computável: o +1 dia cai em
        // 9999-01-01 e o período anterior em 9998-10-31..11-30.
        var client = await ClienteGestorAsync();

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("9998-12-01", "9998-12-31")));

        resumo.Periodo.De.Should().Be(new DateOnly(9998, 12, 1));
        resumo.Periodo.FimExclusivoUtc.Should().Be(
            new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        resumo.PeriodoAnterior.De.Should().Be(new DateOnly(9998, 10, 31));
        resumo.PeriodoAnterior.Ate.Should().Be(new DateOnly(9998, 11, 30));
        resumo.ReceitaBruta.Should().Be(0m);
    }

    [Fact]
    public async Task CONTROLE_POSITIVO_borda_maxima_COMPUTAVEL_devolve_200()
    {
        // O último `ate` que ainda tem "dia seguinte" no calendário. Escrito em LITERAL
        // (9999-12-30), não derivado de `DateOnly.MaxValue.AddDays(-1)` — repetir no teste a
        // aritmética do produto faria a asserção concordar com ele mesmo quando ele errasse.
        var client = await ClienteGestorAsync();

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("9999-12-30", "9999-12-30")));

        resumo.Periodo.FimExclusivoUtc.Should().Be(
            new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        resumo.PeriodoAnterior.De.Should().Be(new DateOnly(9999, 12, 29));
    }

    [Fact]
    public async Task CONTROLE_POSITIVO_duracao_EXATAMENTE_no_teto_devolve_200()
    {
        // O outro lado do teto de volume: 1830 dias contados INCLUSIVE nos dois extremos
        // ainda passam. Sem este caso, o teto poderia estar implementado com `<` no lugar de
        // `<=` (ou um dia fora) e nenhuma asserção perceberia. 2010-01-01 + 1829 dias =
        // 2015-01-04 — literal, conferido no calendário, não derivado da constante.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota("2010-01-01", "2015-01-04"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Intervalo_invertido_devolve_400()
    {
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota("2011-12-31", "2011-01-01"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("não pode ser posterior",
            "o 400 do intervalo invertido tem de dizer o que está errado, e não sair como "
            + "erro genérico de contrato");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-4 — A BORDA SUPERIOR, POR HTTP
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_as_23h59_do_ULTIMO_dia_CONTA_e_a_do_dia_seguinte_NAO()
    {
        // Datas LITERAIS dos dois lados da borda. O caminho HTTP importa aqui porque a
        // conversão data -> instante acontece depois do model binding: um `DateTime` no lugar
        // do `DateOnly` faria o cliente mandar `2013-03-31T00:00:00` e perder o dia inteiro.
        var client = await ClienteGestorAsync();

        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 45.00m,
            "2013-03-31T23:59:00Z");
        await LancarAsync(client, KuraApiFactory.IdSegundoEventoClinicoSemeado, 999.00m,
            "2013-04-01T00:00:00Z");

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("2013-03-01", "2013-03-31")));

        resumo.ReceitaBruta.Should().Be(45.00m,
            "2013-03-31 23:59 está DENTRO de um período que termina em 2013-03-31 inclusive");
        resumo.NrCobrancas.Should().Be(1);

        // A resposta devolve o intervalo semiaberto que usou — literais, não derivados.
        resumo.Periodo.InicioUtc.Should().Be(
            new DateTime(2013, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        resumo.Periodo.FimExclusivoUtc.Should().Be(
            new DateTime(2013, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-9 / R-10 — o mix reconcilia, por HTTP
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Mix_reconcilia_com_a_receita_e_o_avulso_tem_balde_proprio()
    {
        var client = await ClienteGestorAsync();

        // Dois lançamentos no MESMO atendimento, pelo serviço de tabela.
        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 100.00m,
            "2015-05-10T09:00:00Z", KuraApiFactory.IdServicoPrecoSemeado);
        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 50.00m,
            "2015-05-10T09:30:00Z", KuraApiFactory.IdServicoPrecoSemeado);

        // Um lançamento AVULSO em outro atendimento (FK nula, legítimo pela D-2).
        await LancarAsync(client, KuraApiFactory.IdSegundoEventoClinicoSemeado, 25.00m,
            "2015-05-11T09:00:00Z");

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("2015-05-01", "2015-05-31")));

        resumo.ReceitaBruta.Should().Be(175.00m);
        resumo.NrCobrancas.Should().Be(3);
        resumo.NrAtendimentosCobrados.Should().Be(2, "3 lançamentos, 2 atendimentos");
        resumo.TicketMedio.Should().Be(87.50m, "175,00 / 2 atendimentos");

        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(resumo.ReceitaBruta,
            "o mix RECONCILIA — é o invariante da task, e ele atravessa a serialização");

        var doServico = resumo.MixPorServico
            .Single(m => m.IdServicoPreco == KuraApiFactory.IdServicoPrecoSemeado);
        doServico.Receita.Should().Be(150.00m);
        doServico.NmServico.Should().Be(KuraApiFactory.NomeServicoPrecoSemeado);

        var avulso = resumo.MixPorServico.Single(m => m.IdServicoPreco is null);
        avulso.Receita.Should().Be(25.00m);

        // 🔴 F2 da fix wave pós-G2: LITERAL, e não `FinanceiroService.RotuloAvulso` — a
        // asserção anterior era derivada da constante que ela provava, então trocar o valor
        // por "" deixava tudo verde com o app renderizando um balde em branco. Aqui, no
        // caminho HTTP, o literal também prova que o rótulo ATRAVESSA a serialização.
        avulso.NmServico.Should().Be("(avulso)");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-8 — comparação com o período anterior, por HTTP
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Comparacao_com_o_periodo_anterior_usa_o_mes_imediatamente_antes()
    {
        var client = await ClienteGestorAsync();

        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 200.00m,
            "2017-07-15T09:00:00Z");
        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 100.00m,
            "2017-06-15T09:00:00Z");

        var resumo = await LerResumoAsync(
            await client.GetAsync(Rota("2017-07-01", "2017-07-31")));

        resumo.ReceitaBruta.Should().Be(200.00m);
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(100.00m);
        resumo.VariacaoPercentual.Should().Be(100.00m);

        // Julho tem 31 dias, então o período anterior são os 31 dias que terminam em 30/06 —
        // ou seja, começa em 31/05. Literal, não derivado: é o cálculo que está sendo provado.
        resumo.PeriodoAnterior.De.Should().Be(new DateOnly(2017, 5, 31));
        resumo.PeriodoAnterior.Ate.Should().Be(new DateOnly(2017, 6, 30));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-7 — o null atravessa a serialização
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Periodo_sem_cobranca_devolve_200_com_estado_vazio_declarado()
    {
        // Não é 404 (o período existe, só não teve faturamento) e não é 500. E o que separa
        // "não faturou" de "não medimos" são os NULOS — que precisam sobreviver ao JSON.
        var client = await ClienteGestorAsync();

        var resposta = await client.GetAsync(Rota("2019-01-01", "2019-01-31"));
        var resumo = await LerResumoAsync(resposta);

        resumo.ReceitaBruta.Should().Be(0m);
        resumo.NrCobrancas.Should().Be(0);
        resumo.TicketMedio.Should().BeNull();
        resumo.VariacaoPercentual.Should().BeNull();
        resumo.MixPorServico.Should().BeEmpty();

        // 🔴 O null tem de ser null NO JSON, não 0. Um conversor ou um default que trocasse
        // um pelo outro desfaria a ruling inteira sem quebrar nenhum teste de service.
        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("\"ticketMedio\":null");
        corpo.Should().Contain("\"variacaoPercentual\":null");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-11 — IDOR: a isca do outro tenant EXISTE
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_da_OUTRA_clinica_nao_entra_em_nenhum_KPI()
    {
        // 🔴 A isca é a cobrança de 777,77 semeada na clínica 2 pela KuraApiFactory (F3 da
        // revisão G2 da FD-10), com DtCobranca de ONTEM — por isso a janela deste teste é
        // ontem..hoje, e não um dos anos isolados. Sem a isca EXISTINDO no período, "não
        // vazou" seria logicamente incapaz de falhar.
        var client = await ClienteGestorAsync();

        var ontem = DateTime.UtcNow.AddDays(-1);
        var deIso = DateOnly.FromDateTime(ontem).ToString("yyyy-MM-dd");
        var ateIso = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Controle positivo: uma cobrança NOSSA na mesma janela. Sem ela, uma receita zerada
        // seria compatível com "o filtro descarta tudo".
        await LancarAsync(client, KuraApiFactory.IdEventoClinicoSemeado, 11.00m,
            ontem.ToString("yyyy-MM-ddTHH:mm:ssZ"));

        var resumo = await LerResumoAsync(await client.GetAsync(Rota(deIso, ateIso)));

        resumo.ReceitaBruta.Should().Be(11.00m);
        resumo.ReceitaBruta.Should().NotBe(788.77m,
            "788,77 = 11,00 + os 777,77 da clínica 2 — o número que um filtro de tenant "
            + "quebrado produziria, e que parece perfeitamente plausível");

        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(11.00m);
        resumo.MixPorServico.Should().NotContain(
            m => m.IdServicoPreco == KuraApiFactory.IdServicoPrecoOutroTenant);
        resumo.NrAtendimentosCobrados.Should().Be(1,
            "o atendimento da clínica 2 também não pode entrar no denominador do ticket");
    }
}
