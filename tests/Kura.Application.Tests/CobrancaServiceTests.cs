namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.Cobranca;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-10 (ciclo FIN) — lançamento de <see cref="Cobranca"/> num evento clínico.
///
/// <para>
/// 🔴 <b>Estes testes usam os REPOSITÓRIOS REAIS sobre um <c>KuraDbContext</c> InMemory, não
/// fakes.</b> Metade do que a task garante mora no predicado dos repositórios
/// (<c>IgnoreQueryFilters()</c> + <c>IdClinica</c> escrito à mão): um fake que reimplemente
/// esses predicados prova o fake, não o produto — trocar <c>e.IdClinica == idClinica</c> por
/// <c>true</c> continuaria verde. Mesma disciplina de <c>ServicoPrecoServiceTests</c>.
/// </para>
///
/// <para>
/// 🔴 <b>E o <c>IClinicaContext</c> do DbContext é montado com <c>IdClinicaFiltro = null</c>
/// DE PROPÓSITO — ou seja, com os query filters de tenant DESLIGADOS.</b> É o arranjo mais
/// hostil disponível: com o filtro ligado, a linha de outra clínica sumiria sozinha e todo
/// teste de isolamento passaria mesmo que o service não fizesse nada. E o arranjo não é
/// artificial: o filtro desliga inteiro (não nega) sempre que não há clínica no contexto.
/// </para>
/// </summary>
public class CobrancaServiceTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    private const long EventoDaClinicaA = 10L;
    private const long EventoDaClinicaB = 20L;

    private const long ServicoDaClinicaA = 100L;
    private const long ServicoDaClinicaB = 200L;
    private const long ServicoDesativadoDaClinicaA = 300L;

    /// <summary>Contexto com os query filters DESLIGADOS — ver a documentação da classe.</summary>
    private static KuraDbContext CriarContexto(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static CobrancaService CriarService(KuraDbContext ctx, long idClinicaDoJwt)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);

        return new CobrancaService(
            new CobrancaRepository(ctx),
            new EventoClinicoRepository(ctx),
            new ServicoPrecoRepository(ctx),
            new UnitOfWork(ctx),
            clinicaContext.Object);
    }

    /// <summary>
    /// Cenário base dos DOIS tenants. 🔴 A isca do outro tenant é semeada SEMPRE: sem linha
    /// alheia no banco, "evento de outra clínica é recusado" seria logicamente incapaz de
    /// falhar — o 404 viria de a linha não existir para ninguém, não do escopo.
    /// </summary>
    private static KuraDbContext CenarioComOsDoisTenants(string dbName)
    {
        var ctx = CriarContexto(dbName);

        SemearEvento(ctx, EventoDaClinicaA, ClinicaA);
        SemearEvento(ctx, EventoDaClinicaB, ClinicaB);

        SemearServico(ctx, ServicoDaClinicaA, ClinicaA, "Consulta A", 150.00m);
        SemearServico(ctx, ServicoDaClinicaB, ClinicaB, "Consulta B", 999.99m);
        SemearServico(ctx, ServicoDesativadoDaClinicaA, ClinicaA, "Saiu do catálogo", 50.00m, ativo: false);

        return ctx;
    }

    private static void SemearEvento(KuraDbContext ctx, long id, long idClinica)
    {
        var evento = new EventoClinico
        {
            Id = id,
            IdClinica = idClinica,
            IdPet = id,
            IdVeterinario = idClinica,
            IdTipoEvento = 1,
            DtEvento = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            DsObservacao = "Atendimento de teste",
            StAtiva = true,
        };

        ctx.EventosClinicos.Add(evento);
        ctx.SaveChanges();
        ctx.Entry(evento).State = EntityState.Detached;
    }

    private static void SemearServico(
        KuraDbContext ctx, long id, long idClinica, string nome, decimal preco, bool ativo = true)
    {
        var servico = new ServicoPreco
        {
            Id = id,
            IdClinica = idClinica,
            NmServico = nome,
            VlPreco = preco,
            StAtiva = ativo,
        };

        ctx.ServicosPreco.Add(servico);
        ctx.SaveChanges();
        ctx.Entry(servico).State = EntityState.Detached;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 O INVARIANTE CENTRAL: o valor é COPIADO, nunca lido por FK
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_lancada_NAO_muda_quando_o_preco_de_tabela_muda()
    {
        // 🔴 O teste mais importante da FD-10. Se VL_COBRADO fosse resolvido por FK na
        // leitura, o histórico financeiro se reescreveria sozinho a cada remarcação de preço
        // — e os KPI da FD-11 mudariam de resposta sem que nada financeiro tivesse
        // acontecido.
        using var ctx = CenarioComOsDoisTenants(
            nameof(Cobranca_lancada_NAO_muda_quando_o_preco_de_tabela_muda));
        var service = CriarService(ctx, ClinicaA);

        // 1. Lança pelo serviço de tabela, que hoje custa 150,00.
        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDaClinicaA,
        });

        lancada.VlCobrado.Should().Be(150.00m);

        // 2. A tabela de preços é remarcada — o dobro, para que qualquer leitura por FK seja
        //    inconfundível na asserção.
        var servico = await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == ServicoDaClinicaA);
        servico.VlPreco = 300.00m;
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // 3. Relê a cobrança pelo caminho de produto.
        var relida = await service.ObterPorIdAsync(EventoDaClinicaA, lancada.Id);

        relida.VlCobrado.Should().Be(150.00m, "VL_COBRADO é uma CÓPIA do preço do instante do "
            + "lançamento; remarcar a tabela não reescreve histórico financeiro");
        relida.VlCobrado.Should().NotBe(300.00m);

        // 4. E no banco, ignorando qualquer filtro: a coluna guarda mesmo o valor antigo, e a
        //    origem (ID_SERVICO_PRECO) continua apontando o serviço remarcado — rastreabilidade
        //    preservada sem que ela vire fonte de valor.
        var gravada = await ctx.Cobrancas.IgnoreQueryFilters().SingleAsync(c => c.Id == lancada.Id);
        gravada.VlCobrado.Should().Be(150.00m);
        gravada.IdServicoPreco.Should().Be(ServicoDaClinicaA);

        // Controle positivo do instrumento: o preço de tabela REALMENTE mudou. Sem esta
        // asserção, um SaveChanges que não tivesse persistido nada faria o teste passar por
        // vácuo — provando "o valor não mudou" num cenário onde nada mudou.
        var precoAtual = await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == ServicoDaClinicaA);
        precoAtual.VlPreco.Should().Be(300.00m);
    }

    [Fact]
    public async Task Lancamento_por_servico_copia_o_preco_do_instante()
    {
        using var ctx = CenarioComOsDoisTenants(
            nameof(Lancamento_por_servico_copia_o_preco_do_instante));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDaClinicaA,
        });

        lancada.VlCobrado.Should().Be(150.00m);
        lancada.IdServicoPreco.Should().Be(ServicoDaClinicaA);
    }

    [Fact]
    public async Task Valor_informado_ganha_do_preco_de_tabela_e_o_servico_fica_como_origem()
    {
        // D-2: desconto de balcão é lançamento legítimo. O serviço continua gravado como
        // ORIGEM (é o que a FD-11 usa para o mix por serviço), sem virar fonte de valor.
        using var ctx = CenarioComOsDoisTenants(
            nameof(Valor_informado_ganha_do_preco_de_tabela_e_o_servico_fica_como_origem));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDaClinicaA,
            VlCobrado = 120.00m,
        });

        lancada.VlCobrado.Should().Be(120.00m);
        lancada.VlCobrado.Should().NotBe(150.00m);
        lancada.IdServicoPreco.Should().Be(ServicoDaClinicaA);
    }

    [Fact]
    public async Task Lancamento_avulso_grava_sem_servico_de_origem()
    {
        // D-2: valor digitado direto, sem item de catálogo. ID_SERVICO_PRECO é nullable na
        // V18 exatamente para este caso.
        using var ctx = CenarioComOsDoisTenants(
            nameof(Lancamento_avulso_grava_sem_servico_de_origem));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 77.50m,
            DsFormaPagamento = "PIX",
        });

        lancada.VlCobrado.Should().Be(77.50m);
        lancada.IdServicoPreco.Should().BeNull();
        lancada.DsFormaPagamento.Should().Be("PIX");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 Travas de tenant — cada recusa com o seu controle positivo
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Evento_de_OUTRA_clinica_e_recusado()
    {
        // 🔴 A isca EXISTE no banco (CenarioComOsDoisTenants semeia o evento da clínica B).
        // Sem ela, este 404 viria de "não existe para ninguém" e não provaria escopo nenhum.
        // A FK_COBRANCA_EVENTO da V18 não compõe com ID_CLINICA: sem a comparação explícita
        // do service, este lançamento penduraria receita da clínica A num atendimento da B.
        using var ctx = CenarioComOsDoisTenants(nameof(Evento_de_OUTRA_clinica_e_recusado));
        var service = CriarService(ctx, ClinicaA);

        var acao = () => service.LancarAsync(EventoDaClinicaB, new CobrancaCreateDto
        {
            VlCobrado = 100.00m,
        });

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        // E nada foi gravado — a recusa não deixou linha órfã.
        (await ctx.Cobrancas.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Evento_da_PROPRIA_clinica_e_aceito()
    {
        // 🔴 CONTROLE POSITIVO do teste acima. Sem ele, um service que recusasse TODO evento
        // (repositório quebrado, predicado invertido, contexto vazio) passaria naquele teste.
        using var ctx = CenarioComOsDoisTenants(nameof(Evento_da_PROPRIA_clinica_e_aceito));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 100.00m,
        });

        lancada.IdEventoClinico.Should().Be(EventoDaClinicaA);
        lancada.IdClinica.Should().Be(ClinicaA);

        var gravada = await ctx.Cobrancas.IgnoreQueryFilters().SingleAsync();
        gravada.IdClinica.Should().Be(ClinicaA);
        gravada.IdEventoClinico.Should().Be(EventoDaClinicaA);
    }

    [Fact]
    public async Task O_MESMO_evento_e_recusado_para_a_clinica_alheia_e_aceito_para_a_dona()
    {
        // A forma mais forte do par acima: MESMO id de evento, dois JWTs diferentes, banco
        // idêntico. O que muda entre o 404 e o 201 é exclusivamente a clínica do token.
        using var ctx = CenarioComOsDoisTenants(
            nameof(O_MESMO_evento_e_recusado_para_a_clinica_alheia_e_aceito_para_a_dona));

        var comoClinicaA = CriarService(ctx, ClinicaA);
        var comoClinicaB = CriarService(ctx, ClinicaB);

        var corpo = () => new CobrancaCreateDto { VlCobrado = 10.00m };

        var recusa = () => comoClinicaA.LancarAsync(EventoDaClinicaB, corpo());
        await recusa.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        var aceita = await comoClinicaB.LancarAsync(EventoDaClinicaB, corpo());
        aceita.IdClinica.Should().Be(ClinicaB);
    }

    [Fact]
    public async Task ServicoPreco_de_OUTRA_clinica_e_recusado()
    {
        using var ctx = CenarioComOsDoisTenants(nameof(ServicoPreco_de_OUTRA_clinica_e_recusado));
        var service = CriarService(ctx, ClinicaA);

        // Evento PRÓPRIO (o caminho já provado bom), serviço ALHEIO: o que este teste isola é
        // a segunda trava, e só ela.
        var acao = () => service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDaClinicaB,
        });

        (await acao.Should().ThrowAsync<RegraDeNegocioException>())
            .WithMessage(CobrancaService.MensagemServicoIndisponivel);

        // 🔴 A prova de que a recusa NÃO foi por acaso de valor: o serviço da clínica B custa
        // 999,99. Se o predicado de tenant do repositório fosse removido, o lançamento
        // passaria e gravaria esse número.
        (await ctx.Cobrancas.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ServicoPreco_da_PROPRIA_clinica_e_aceito()
    {
        // 🔴 CONTROLE POSITIVO dos dois testes de serviço (alheio e desativado).
        using var ctx = CenarioComOsDoisTenants(nameof(ServicoPreco_da_PROPRIA_clinica_e_aceito));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDaClinicaA,
        });

        lancada.IdServicoPreco.Should().Be(ServicoDaClinicaA);
        lancada.VlCobrado.Should().Be(150.00m);
    }

    [Fact]
    public async Task ServicoPreco_DESATIVADO_e_recusado()
    {
        using var ctx = CenarioComOsDoisTenants(nameof(ServicoPreco_DESATIVADO_e_recusado));
        var service = CriarService(ctx, ClinicaA);

        var acao = () => service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            IdServicoPreco = ServicoDesativadoDaClinicaA,
        });

        // Mensagem DIFERENTE da do serviço inexistente/alheio, de propósito: o desativado é
        // desta clínica, então dizer o motivo não vaza nada e evita que o gestor procure um
        // id que ele tem.
        (await acao.Should().ThrowAsync<RegraDeNegocioException>())
            .WithMessage(CobrancaService.MensagemServicoDesativado);
    }

    [Fact]
    public async Task Lancamento_grava_a_clinica_do_JWT_e_nada_mais()
    {
        using var ctx = CenarioComOsDoisTenants(
            nameof(Lancamento_grava_a_clinica_do_JWT_e_nada_mais));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 33.00m,
        });

        lancada.IdClinica.Should().Be(ClinicaA);
        lancada.IdClinica.Should().NotBe(ClinicaB);

        var gravada = await ctx.Cobrancas.IgnoreQueryFilters().SingleAsync(c => c.Id == lancada.Id);
        gravada.IdClinica.Should().Be(ClinicaA);
        gravada.StAtiva.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Leitura — o mesmo escopo, pelo caminho de leitura
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_do_evento_alheio_e_recusado_e_o_do_proprio_evento_lista()
    {
        using var ctx = CenarioComOsDoisTenants(
            nameof(Listar_do_evento_alheio_e_recusado_e_o_do_proprio_evento_lista));

        var comoClinicaA = CriarService(ctx, ClinicaA);
        var comoClinicaB = CriarService(ctx, ClinicaB);

        await comoClinicaA.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto { VlCobrado = 11m });
        await comoClinicaB.LancarAsync(EventoDaClinicaB, new CobrancaCreateDto { VlCobrado = 22m });

        // Recusa: listar cobranças de um atendimento que não é seu devolve 404, e NÃO lista
        // vazia — lista vazia seria indistinguível de "este atendimento não teve cobrança",
        // uma afirmação sobre um atendimento alheio.
        var recusa = () => comoClinicaA.ListarDoEventoAsync(EventoDaClinicaB);
        await recusa.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        // Controle positivo: o próprio evento lista, e lista só o que é dele.
        var lista = (await comoClinicaA.ListarDoEventoAsync(EventoDaClinicaA)).ToList();
        lista.Should().HaveCount(1);
        lista.Should().OnlyContain(c => c.IdClinica == ClinicaA);
        lista.Should().OnlyContain(c => c.VlCobrado == 11m);
    }

    [Fact]
    public async Task Obter_cobranca_de_outra_clinica_por_id_e_recusado()
    {
        using var ctx = CenarioComOsDoisTenants(
            nameof(Obter_cobranca_de_outra_clinica_por_id_e_recusado));

        var comoClinicaA = CriarService(ctx, ClinicaA);
        var comoClinicaB = CriarService(ctx, ClinicaB);

        var daClinicaB = await comoClinicaB.LancarAsync(
            EventoDaClinicaB, new CobrancaCreateDto { VlCobrado = 22m });

        // IDOR direto: a clínica A pede o id da cobrança da B.
        var recusa = () => comoClinicaA.ObterPorIdAsync(EventoDaClinicaB, daClinicaB.Id);
        await recusa.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        // 🔴 Controle positivo: a MESMA leitura, pelo dono, devolve a linha. Sem ele, um
        // ObterPorIdAsync que lançasse sempre passaria na recusa acima.
        var dona = await comoClinicaB.ObterPorIdAsync(EventoDaClinicaB, daClinicaB.Id);
        dona.Id.Should().Be(daClinicaB.Id);
        dona.IdClinica.Should().Be(ClinicaB);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 F1 da revisão G2 — o idEventoClinico da ROTA participa da busca
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Obter_cobranca_por_um_evento_que_NAO_e_o_dela_e_recusado()
    {
        // 🔴 F1: antes do fix o predicado era só (id + clínica) e o segmento do meio da rota
        // era aceito com QUALQUER valor. Aqui a cobrança é legitimamente da clínica A e a
        // leitura é feita pela clínica A — o único erro é o evento. Se este teste passar a
        // devolver a linha, o `idEventoClinico` voltou a ser decoração.
        using var ctx = CenarioComOsDoisTenants(
            nameof(Obter_cobranca_por_um_evento_que_NAO_e_o_dela_e_recusado));
        var service = CriarService(ctx, ClinicaA);

        // Um SEGUNDO evento da MESMA clínica: assim o que separa o 404 do 200 é
        // exclusivamente o evento, sem nenhuma trava de tenant ajudando.
        const long OutroEventoDaClinicaA = 11L;
        SemearEvento(ctx, OutroEventoDaClinicaA, ClinicaA);

        var lancada = await service.LancarAsync(
            EventoDaClinicaA, new CobrancaCreateDto { VlCobrado = 50m });

        var recusa = () => service.ObterPorIdAsync(OutroEventoDaClinicaA, lancada.Id);
        await recusa.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        // 🔴 CONTROLE POSITIVO: a MESMA cobrança, pelo evento certo, volta.
        var certa = await service.ObterPorIdAsync(EventoDaClinicaA, lancada.Id);
        certa.Id.Should().Be(lancada.Id);
    }

    [Fact]
    public async Task Obter_cobranca_por_um_evento_INEXISTENTE_e_recusado()
    {
        using var ctx = CenarioComOsDoisTenants(
            nameof(Obter_cobranca_por_um_evento_INEXISTENTE_e_recusado));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(
            EventoDaClinicaA, new CobrancaCreateDto { VlCobrado = 50m });

        // 999999 não existe para ninguém — medido como 200 antes do fix.
        var recusa = () => service.ObterPorIdAsync(999999L, lancada.Id);
        await recusa.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        (await service.ObterPorIdAsync(EventoDaClinicaA, lancada.Id)).Id.Should().Be(lancada.Id);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // DT_COBRANCA — a data que não pode nascer 0001-01-01
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DtCobranca_ausente_vira_agora_e_NUNCA_o_default_de_DateTime()
    {
        // 🔴 Achado F2 da revisão G2 da FD-08: DateTime é struct, então "esquecer de setar"
        // não produz nulo, produz 0001-01-01 — valor que passa pelo NOT NULL do Oracle e
        // some de todo KPI por período da FD-11. Receita lançada, gravada e invisível.
        using var ctx = CenarioComOsDoisTenants(
            nameof(DtCobranca_ausente_vira_agora_e_NUNCA_o_default_de_DateTime));
        var service = CriarService(ctx, ClinicaA);

        var antes = DateTime.UtcNow.AddSeconds(-5);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 10m,
        });

        lancada.DtCobranca.Should().NotBe(default);
        lancada.DtCobranca.Should().BeOnOrAfter(antes);
        lancada.DtCobranca.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));

        var gravada = await ctx.Cobrancas.IgnoreQueryFilters().SingleAsync(c => c.Id == lancada.Id);
        gravada.DtCobranca.Should().NotBe(default);
    }

    [Fact]
    public async Task DtCobranca_retroativa_informada_e_preservada()
    {
        // O caso real que justifica aceitar data do cliente: o fechamento do dia anterior
        // lançado na manhã seguinte. A faixa aceita é validada em CobrancaCreateValidator.
        using var ctx = CenarioComOsDoisTenants(
            nameof(DtCobranca_retroativa_informada_e_preservada));
        var service = CriarService(ctx, ClinicaA);

        var ontem = DateTime.UtcNow.Date.AddDays(-1).AddHours(18);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 10m,
            DtCobranca = ontem,
        });

        lancada.DtCobranca.Should().Be(ontem);
    }

    [Fact]
    public async Task Forma_de_pagamento_em_branco_vira_nulo()
    {
        // DS_FORMA_PAGAMENTO é nullable na V18. String vazia gravada seria um valor que não é
        // nulo e não significa nada — pior de ler que a ausência.
        using var ctx = CenarioComOsDoisTenants(nameof(Forma_de_pagamento_em_branco_vira_nulo));
        var service = CriarService(ctx, ClinicaA);

        var lancada = await service.LancarAsync(EventoDaClinicaA, new CobrancaCreateDto
        {
            VlCobrado = 10m,
            DsFormaPagamento = "   ",
        });

        lancada.DsFormaPagamento.Should().BeNull();
    }
}
