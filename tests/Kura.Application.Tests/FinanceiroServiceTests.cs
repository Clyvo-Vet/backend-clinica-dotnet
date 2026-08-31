namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-11 (ciclo FIN) — os 4 KPI financeiros da clínica.
///
/// <para>
/// 🔴 <b>Repositórios REAIS sobre um <c>KuraDbContext</c> InMemory, não fakes.</b> Metade do
/// que a task garante mora no predicado do repositório — <c>IgnoreQueryFilters()</c> +
/// <c>IdClinica</c> escrito à mão + a faixa <b>semiaberta</b> de datas. Um fake que
/// reimplementasse esses predicados provaria o fake: trocar <c>c.IdClinica == idClinica</c>
/// por <c>true</c>, ou <c>&lt; fimExclusivo</c> por <c>&lt;= fimExclusivo</c>, continuaria
/// verde. Mesma disciplina de <c>CobrancaServiceTests</c>.
/// </para>
///
/// <para>
/// 🔴 <b>O <c>IClinicaContext</c> do DbContext é montado com <c>IdClinicaFiltro = null</c> DE
/// PROPÓSITO</b> — ou seja, com os query filters de tenant DESLIGADOS. É o arranjo mais
/// hostil disponível: com o filtro ligado, a linha da outra clínica sumiria sozinha e todo
/// teste de isolamento passaria mesmo que o service não fizesse nada. E não é artificial: o
/// filtro desliga inteiro sempre que não há clínica no contexto.
/// </para>
///
/// <para>
/// 🔴 <b>TODA asserção de borda é escrita contra DATA LITERAL, nunca derivada da aritmética
/// que está sendo provada.</b> Lição paga em retrabalho na revisão G2 da FD-10: o teste da
/// tolerância futura tinha sido escrito em termos da própria constante e por isso era
/// <b>incapaz</b> de detectar que a constante mudara — a mutação de 1 dia para 7 deixou os 19
/// casos verdes. Um <c>ate.AddDays(1).AddTicks(-1)</c> aqui teria o mesmo defeito: repetiria
/// no teste o cálculo do produto e concordaria com ele mesmo quando ele estivesse errado.
/// </para>
///
/// <para>
/// <b>Nenhum <c>EventoClinico</c> é semeado, e isso é deliberado:</b> o
/// <see cref="FinanceiroService"/> nunca consulta a tabela de eventos — ele conta
/// <c>ID_EVENTO_CLINICO</c> <b>distinto</b> dentro das cobranças. Semear eventos daria a
/// impressão de que existe uma junção que não existe (e que a FD-08 evitou de propósito, pelo
/// episódio da TASK-63/FIX_4, em que o filtro da entidade referenciada derrubava a linha pai).
/// </para>
/// </summary>
public class FinanceiroServiceTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    private const long ServicoConsultaA = 100L;
    private const long ServicoVacinaA = 101L;
    private const long ServicoDesativadoA = 102L;
    private const long ServicoDaClinicaB = 200L;

    // ── O PERÍODO DE REFERÊNCIA DE TODOS OS TESTES, EM LITERAL ───────────────────────────
    // 3 dias: 10, 11 e 12 de maio de 2026.
    private static readonly DateOnly De = new(2026, 5, 10);
    private static readonly DateOnly Ate = new(2026, 5, 12);

    // O período anterior correspondente, escrito à mão e NÃO derivado: 7, 8 e 9 de maio.
    private static readonly DateOnly AnteriorDe = new(2026, 5, 7);
    private static readonly DateOnly AnteriorAte = new(2026, 5, 9);

    // Instantes de borda, todos literais.
    private static readonly DateTime PrimeiroInstanteDoPeriodo = Utc(2026, 5, 10, 0, 0, 0);
    private static readonly DateTime UltimoMinutoDoPeriodo = Utc(2026, 5, 12, 23, 59, 0);
    private static readonly DateTime MeiaNoiteDoDiaSeguinte = Utc(2026, 5, 13, 0, 0, 0);
    private static readonly DateTime UltimoMinutoDoAnterior = Utc(2026, 5, 9, 23, 59, 0);
    private static readonly DateTime PrimeiroInstanteDoAnterior = Utc(2026, 5, 7, 0, 0, 0);
    private static readonly DateTime VesperaDoAnterior = Utc(2026, 5, 6, 23, 59, 0);

    // Um instante confortavelmente NO MEIO do período, para os cenários que não são de borda.
    private static readonly DateTime MeioDoPeriodo = Utc(2026, 5, 11, 12, 0, 0);
    private static readonly DateTime MeioDoAnterior = Utc(2026, 5, 8, 12, 0, 0);

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto, int segundo) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Arranjo
    // ─────────────────────────────────────────────────────────────────────────────────────

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

    private static FinanceiroService CriarService(KuraDbContext ctx, long idClinicaDoJwt)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);

        return new FinanceiroService(
            new CobrancaRepository(ctx),
            new ServicoPrecoRepository(ctx),
            clinicaContext.Object);
    }

    /// <summary>
    /// Catálogo dos DOIS tenants. O serviço da clínica B existe só como isca de rótulo: se o
    /// predicado de clínica de <c>ListarPorIdsNaClinicaAsync</c> cair, o nome dele aparece no
    /// mix da clínica A.
    /// </summary>
    private static KuraDbContext CenarioBase(string dbName)
    {
        var ctx = CriarContexto(dbName);

        SemearServico(ctx, ServicoConsultaA, ClinicaA, "Consulta clínica");
        SemearServico(ctx, ServicoVacinaA, ClinicaA, "Vacina V10");
        SemearServico(ctx, ServicoDesativadoA, ClinicaA, "Banho e tosa (saiu do catálogo)");
        SemearServico(ctx, ServicoDaClinicaB, ClinicaB, "Serviço do outro tenant");

        return ctx;
    }

    private static void SemearServico(
        KuraDbContext ctx, long id, long idClinica, string nome, bool ativo = true)
    {
        var servico = new ServicoPreco
        {
            Id = id,
            IdClinica = idClinica,
            NmServico = nome,
            VlPreco = 100.00m,
            StAtiva = ativo,
        };

        ctx.ServicosPreco.Add(servico);
        ctx.SaveChanges();
        ctx.Entry(servico).State = EntityState.Detached;
    }

    /// <summary>
    /// Semeia uma cobrança direto na tabela.
    ///
    /// <para>⚠️ <c>dtCriacao</c> é parâmetro separado de <c>dtCobranca</c> de propósito: a
    /// divergência entre as duas é o que trava a chave de agregação (R-13). Por padrão elas
    /// coincidem — que é justamente o cenário em que trocar uma pela outra passaria
    /// despercebido.</para>
    /// </summary>
    private static void SemearCobranca(
        KuraDbContext ctx,
        long id,
        long idEventoClinico,
        long idClinica,
        decimal valor,
        DateTime dtCobranca,
        long? idServicoPreco = null,
        bool ativa = true,
        DateTime? dtCriacao = null)
    {
        var cobranca = new Cobranca
        {
            Id = id,
            IdEventoClinico = idEventoClinico,
            IdClinica = idClinica,
            IdServicoPreco = idServicoPreco,
            VlCobrado = valor,
            DtCobranca = dtCobranca,
            DtCriacao = dtCriacao ?? dtCobranca,
            StAtiva = ativa,
        };

        ctx.Cobrancas.Add(cobranca);
        ctx.SaveChanges();
        ctx.Entry(cobranca).State = EntityState.Detached;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-4 — A BORDA SUPERIOR: o último dia conta INTEIRO
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_as_23h59_do_ULTIMO_dia_do_periodo_CONTA()
    {
        // 🔴 A armadilha central da task. As datas do gestor são INCLUSIVAS; um filtro
        // `DtCobranca <= ate` compara contra 2026-05-12 00:00:00 e descarta as 23h59 finais
        // do dia 12 — receita real que some sem erro, sem log, com o total ainda parecendo
        // plausível. A asserção é contra o instante LITERAL, não contra `ate.AddDays(1)`.
        using var ctx = CenarioBase(nameof(Cobranca_as_23h59_do_ULTIMO_dia_do_periodo_CONTA));
        SemearCobranca(ctx, 1, 10, ClinicaA, 40.00m, UltimoMinutoDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(40.00m,
            "2026-05-12 23:59 está DENTRO de um período que vai até 2026-05-12 inclusive");
        resumo.NrCobrancas.Should().Be(1);
        resumo.NrAtendimentosCobrados.Should().Be(1);
    }

    [Fact]
    public async Task Cobranca_na_MEIA_NOITE_do_dia_seguinte_ao_fim_NAO_conta()
    {
        // O outro lado da mesma borda: o fim é EXCLUSIVO. Sem esta asserção, "o último dia
        // conta inteiro" poderia estar implementado como `<= ate+1d`, que engoliria o
        // primeiro instante do dia 13.
        using var ctx = CenarioBase(nameof(Cobranca_na_MEIA_NOITE_do_dia_seguinte_ao_fim_NAO_conta));
        SemearCobranca(ctx, 1, 10, ClinicaA, 40.00m, UltimoMinutoDoPeriodo);
        SemearCobranca(ctx, 2, 11, ClinicaA, 999.00m, MeiaNoiteDoDiaSeguinte);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        // A parcela de 40,00 é o controle positivo: prova que o instrumento enxerga alguma
        // coisa. Um zero aqui seria compatível com "o filtro descarta tudo".
        resumo.ReceitaBruta.Should().Be(40.00m);
        resumo.NrCobrancas.Should().Be(1);
    }

    [Fact]
    public async Task Cobranca_no_PRIMEIRO_instante_do_periodo_conta()
    {
        using var ctx = CenarioBase(nameof(Cobranca_no_PRIMEIRO_instante_do_periodo_conta));
        SemearCobranca(ctx, 1, 10, ClinicaA, 25.00m, PrimeiroInstanteDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(25.00m, "o início do período é INCLUSIVO");
    }

    [Fact]
    public async Task A_resposta_devolve_o_intervalo_semiaberto_que_o_filtro_usou_em_UTC()
    {
        // R-5: o período é devolvido para que o app confira a borda em vez de acreditar nela.
        // Instantes LITERAIS — o teste não repete a aritmética do produto.
        using var ctx = CenarioBase(nameof(A_resposta_devolve_o_intervalo_semiaberto_que_o_filtro_usou_em_UTC));

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.Periodo.De.Should().Be(new DateOnly(2026, 5, 10));
        resumo.Periodo.Ate.Should().Be(new DateOnly(2026, 5, 12));
        resumo.Periodo.InicioUtc.Should().Be(Utc(2026, 5, 10, 0, 0, 0));
        resumo.Periodo.FimExclusivoUtc.Should().Be(Utc(2026, 5, 13, 0, 0, 0));

        // Kind explícito: sem ele o JSON sai sem o sufixo Z e o app lê o instante como hora
        // local dele — a ambiguidade de fuso que a task se recusa a introduzir.
        resumo.Periodo.InicioUtc.Kind.Should().Be(DateTimeKind.Utc);
        resumo.Periodo.FimExclusivoUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Periodo_de_UM_dia_cobre_o_dia_inteiro()
    {
        // de == ate é o fechamento diário, o caso mais comum. Ele é válido, e cobre as 24h.
        using var ctx = CenarioBase(nameof(Periodo_de_UM_dia_cobre_o_dia_inteiro));
        SemearCobranca(ctx, 1, 10, ClinicaA, 10.00m, Utc(2026, 5, 11, 0, 0, 0));
        SemearCobranca(ctx, 2, 11, ClinicaA, 20.00m, Utc(2026, 5, 11, 23, 59, 59));
        SemearCobranca(ctx, 3, 12, ClinicaA, 999.00m, Utc(2026, 5, 12, 0, 0, 0));

        var resumo = await CriarService(ctx, ClinicaA)
            .ObterResumoAsync(new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 11));

        resumo.ReceitaBruta.Should().Be(30.00m);
        resumo.Periodo.InicioUtc.Should().Be(Utc(2026, 5, 11, 0, 0, 0));
        resumo.Periodo.FimExclusivoUtc.Should().Be(Utc(2026, 5, 12, 0, 0, 0));

        // E o período anterior de um dia é o dia anterior — literal, não derivado.
        resumo.PeriodoAnterior.De.Should().Be(new DateOnly(2026, 5, 10));
        resumo.PeriodoAnterior.Ate.Should().Be(new DateOnly(2026, 5, 10));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-13 — A CHAVE DE AGREGAÇÃO É DT_COBRANCA, NÃO DT_CRIACAO
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Agrega_por_DT_COBRANCA_e_nao_pela_data_de_criacao_da_linha()
    {
        // 🔴 O controle durável que a FD-10 deve à FD-11 (revisão G2 da FD-10, commit
        // 79878fa). As duas datas DIVERGEM aqui de propósito: no cenário comum elas
        // coincidem, e é por isso que trocar o filtro para DtCriacao passaria despercebido
        // por quase toda a suíte.
        using var ctx = CenarioBase(nameof(Agrega_por_DT_COBRANCA_e_nao_pela_data_de_criacao_da_linha));

        // (a) O fechamento do dia anterior, lançado depois: o FATO é do dia 11, a LINHA
        //     nasceu no dia 30. Conta, porque a receita aconteceu no dia 11.
        SemearCobranca(ctx, 1, 10, ClinicaA, 70.00m,
            dtCobranca: MeioDoPeriodo,
            dtCriacao: Utc(2026, 5, 30, 9, 0, 0));

        // (b) O inverso: a LINHA nasceu dentro do período, o FATO é de depois dele. Não
        //     conta. (Este é o caso que a tolerância futura de +1 dia da FD-10 permite
        //     existir — ela aceita dtCobranca adiantada, e quem agrega tem de honrar o
        //     campo, não a data de gravação.)
        SemearCobranca(ctx, 2, 11, ClinicaA, 999.00m,
            dtCobranca: Utc(2026, 5, 20, 9, 0, 0),
            dtCriacao: MeioDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(70.00m,
            "a agregação é pela data do FATO (DT_COBRANCA), nunca pela data de criação da linha");
        resumo.NrCobrancas.Should().Be(1);
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(70.00m);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-6 — TICKET MÉDIO DIVIDE POR ATENDIMENTO, NÃO POR LANÇAMENTO
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ticket_medio_divide_por_ATENDIMENTO_distinto_nao_por_lancamento()
    {
        // Um atendimento com consulta + vacina + medicamento são 3 cobranças e UM ticket.
        // Dividir por cobrança responderia outra pergunta ("valor médio do item"), sempre
        // menor — o gestor leria queda de ticket onde houve aumento de itens.
        using var ctx = CenarioBase(nameof(Ticket_medio_divide_por_ATENDIMENTO_distinto_nao_por_lancamento));

        // Atendimento 10: três lançamentos, 90,00 no total.
        SemearCobranca(ctx, 1, 10, ClinicaA, 30.00m, MeioDoPeriodo, ServicoConsultaA);
        SemearCobranca(ctx, 2, 10, ClinicaA, 30.00m, MeioDoPeriodo, ServicoVacinaA);
        SemearCobranca(ctx, 3, 10, ClinicaA, 30.00m, MeioDoPeriodo);

        // Atendimento 11: um lançamento, 90,00.
        SemearCobranca(ctx, 4, 11, ClinicaA, 90.00m, MeioDoPeriodo, ServicoConsultaA);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(180.00m);
        resumo.NrCobrancas.Should().Be(4, "são 4 LANÇAMENTOS");
        resumo.NrAtendimentosCobrados.Should().Be(2, "são 2 ATENDIMENTOS");

        resumo.TicketMedio.Should().Be(90.00m, "180,00 / 2 atendimentos");
        resumo.TicketMedio.Should().NotBe(45.00m,
            "45,00 seria 180,00 / 4 LANÇAMENTOS — a divisão errada, e a que parece plausível");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-7 — AS DUAS DIVISÕES DEVOLVEM null, NUNCA 0
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ticket_medio_e_NULL_quando_nao_houve_atendimento_cobrado()
    {
        // Zero atendimentos não tem ticket médio. Devolver 0 afirmaria "o atendimento médio
        // valeu R$ 0,00" — falso, e o ponto sumiria no gráfico junto de um mês ruim de
        // verdade.
        using var ctx = CenarioBase(nameof(Ticket_medio_e_NULL_quando_nao_houve_atendimento_cobrado));

        // Controle positivo: existe cobrança no banco, ela só está FORA do período. Sem esta
        // linha o cenário seria "banco vazio", e um filtro que descartasse tudo passaria.
        SemearCobranca(ctx, 1, 10, ClinicaA, 500.00m, VesperaDoAnterior);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.TicketMedio.Should().BeNull();
        resumo.NrAtendimentosCobrados.Should().Be(0);
        resumo.ReceitaBruta.Should().Be(0m);
    }

    [Fact]
    public async Task Variacao_percentual_e_NULL_quando_a_receita_anterior_e_zero_e_o_numero_cru_vai_junto()
    {
        // Crescer do zero não tem porcentagem. E o caso omitido não daria número estranho:
        // decimal lança DivideByZeroException (não Infinity como double), então a guarda
        // ausente seria 500 no primeiro mês de uso de qualquer clínica.
        using var ctx = CenarioBase(
            nameof(Variacao_percentual_e_NULL_quando_a_receita_anterior_e_zero_e_o_numero_cru_vai_junto));
        SemearCobranca(ctx, 1, 10, ClinicaA, 4200.00m, MeioDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.VariacaoPercentual.Should().BeNull();

        // 🔴 Os números crus vão na resposta justamente para o app ter algo honesto a dizer
        // quando a porcentagem é nula: "de R$ 0,00 para R$ 4.200,00".
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(0m);
        resumo.NrAtendimentosCobradosPeriodoAnterior.Should().Be(0);
        resumo.ReceitaBruta.Should().Be(4200.00m);
    }

    [Fact]
    public async Task Variacao_percentual_e_CALCULADA_quando_ha_base_de_comparacao()
    {
        // Controle positivo das duas asserções de null acima: sem ele, um `return null`
        // incondicional passaria nos dois testes anteriores.
        using var ctx = CenarioBase(nameof(Variacao_percentual_e_CALCULADA_quando_ha_base_de_comparacao));
        SemearCobranca(ctx, 1, 10, ClinicaA, 150.00m, MeioDoPeriodo);
        SemearCobranca(ctx, 2, 20, ClinicaA, 100.00m, MeioDoAnterior);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(150.00m);
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(100.00m);
        resumo.VariacaoPercentual.Should().Be(50.00m, "(150 - 100) / 100 * 100");
        resumo.NrAtendimentosCobradosPeriodoAnterior.Should().Be(1);
    }

    [Fact]
    public async Task Variacao_percentual_NEGATIVA_quando_a_receita_cai()
    {
        using var ctx = CenarioBase(nameof(Variacao_percentual_NEGATIVA_quando_a_receita_cai));
        SemearCobranca(ctx, 1, 10, ClinicaA, 80.00m, MeioDoPeriodo);
        SemearCobranca(ctx, 2, 20, ClinicaA, 100.00m, MeioDoAnterior);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.VariacaoPercentual.Should().Be(-20.00m);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-8 — PERÍODO ANTERIOR: MESMA DURAÇÃO, IMEDIATAMENTE ANTES, SEM SOBREPOR
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Periodo_anterior_tem_a_mesma_duracao_e_termina_na_VESPERA_do_periodo()
    {
        // Datas literais: 10-12/05 (3 dias) tem como anterior 07-09/05 (3 dias). O erro
        // clássico é fazer o anterior terminar EM `de`, contando o primeiro dia do período
        // atual duas vezes — na receita e na base de comparação — e deflacionando a variação.
        using var ctx = CenarioBase(nameof(Periodo_anterior_tem_a_mesma_duracao_e_termina_na_VESPERA_do_periodo));

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.PeriodoAnterior.De.Should().Be(AnteriorDe);
        resumo.PeriodoAnterior.Ate.Should().Be(AnteriorAte);
        resumo.PeriodoAnterior.InicioUtc.Should().Be(Utc(2026, 5, 7, 0, 0, 0));
        resumo.PeriodoAnterior.FimExclusivoUtc.Should().Be(Utc(2026, 5, 10, 0, 0, 0));

        // Contíguos e disjuntos: o fim exclusivo do anterior é exatamente o início do atual.
        resumo.PeriodoAnterior.FimExclusivoUtc.Should().Be(resumo.Periodo.InicioUtc);
    }

    [Fact]
    public async Task Os_dois_periodos_NAO_se_sobrepoem_nem_por_um_instante()
    {
        // A mordida do R-8: três cobranças de valores inconfundíveis nas três bordas.
        using var ctx = CenarioBase(nameof(Os_dois_periodos_NAO_se_sobrepoem_nem_por_um_instante));

        // Primeiro instante do período atual — só pode contar no ATUAL.
        SemearCobranca(ctx, 1, 10, ClinicaA, 7.00m, PrimeiroInstanteDoPeriodo);

        // Último minuto do anterior — só pode contar no ANTERIOR.
        SemearCobranca(ctx, 2, 20, ClinicaA, 300.00m, UltimoMinutoDoAnterior);

        // Primeiro instante do anterior — conta no ANTERIOR (borda inferior inclusiva).
        SemearCobranca(ctx, 3, 21, ClinicaA, 100.00m, PrimeiroInstanteDoAnterior);

        // Véspera do anterior — não conta em NENHUM dos dois.
        SemearCobranca(ctx, 4, 22, ClinicaA, 9999.00m, VesperaDoAnterior);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(7.00m,
            "só a cobrança do primeiro instante do período atual entra na receita");
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(400.00m,
            "300,00 do último minuto + 100,00 do primeiro instante do período anterior");
        resumo.NrAtendimentosCobradosPeriodoAnterior.Should().Be(2);

        // Nem o 7,00 vazou para a base, nem o 9999,00 entrou em lugar nenhum.
        resumo.ReceitaBrutaPeriodoAnterior.Should().NotBe(407.00m);
        (resumo.ReceitaBruta + resumo.ReceitaBrutaPeriodoAnterior).Should().Be(407.00m);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-9 / R-10 — O MIX RECONCILIA
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Mix_por_servico_RECONCILIA_com_a_receita_bruta_incluindo_o_balde_avulso()
    {
        // 🔴 O invariante da task: soma(mix.receita) == receitaBruta, EXATO. É o que dá ao
        // gestor o direito de somar os pedaços.
        using var ctx = CenarioBase(nameof(Mix_por_servico_RECONCILIA_com_a_receita_bruta_incluindo_o_balde_avulso));

        SemearCobranca(ctx, 1, 10, ClinicaA, 150.00m, MeioDoPeriodo, ServicoConsultaA);
        SemearCobranca(ctx, 2, 11, ClinicaA, 150.00m, MeioDoPeriodo, ServicoConsultaA);
        SemearCobranca(ctx, 3, 12, ClinicaA, 80.00m, MeioDoPeriodo, ServicoVacinaA);

        // Lançamento AVULSO (FK nula) — legítimo pela D-2, e o balde que uma agregação
        // descuidada descarta.
        SemearCobranca(ctx, 4, 13, ClinicaA, 33.33m, MeioDoPeriodo, idServicoPreco: null);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(413.33m);
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(resumo.ReceitaBruta,
            "o mix RECONCILIA: toda cobrança do período cai em exatamente um balde");
        resumo.MixPorServico.Sum(m => m.NrCobrancas).Should().Be(resumo.NrCobrancas);

        // Baldes nomeados, maior primeiro.
        resumo.MixPorServico.Should().HaveCount(3);
        resumo.MixPorServico[0].IdServicoPreco.Should().Be(ServicoConsultaA);
        resumo.MixPorServico[0].NmServico.Should().Be("Consulta clínica");
        resumo.MixPorServico[0].Receita.Should().Be(300.00m);
        resumo.MixPorServico[0].NrCobrancas.Should().Be(2);

        // 🔴 O avulso tem balde PRÓPRIO e declarado — nem descartado, nem somado a outro.
        var avulso = resumo.MixPorServico.Single(m => m.IdServicoPreco is null);

        // 🔴 F2 da fix wave pós-G2: LITERAL, não `FinanceiroService.RotuloAvulso`. A
        // asserção anterior era derivada da constante que ela deveria estar provando — trocar
        // "(avulso)" por "" deixava a suíte inteira verde e o app renderizava um balde EM
        // BRANCO no mix, sem nenhum gate perceber. É a mesma armadilha documentada no
        // cabeçalho desta classe para as DATAS; o rótulo tinha ficado de fora.
        avulso.NmServico.Should().Be("(avulso)");
        avulso.Receita.Should().Be(33.33m);
    }

    [Fact]
    public async Task Servico_DESATIVADO_depois_de_faturar_continua_no_mix_com_o_nome_e_o_total_fecha()
    {
        // 🔴 R-10. O nome do serviço é RÓTULO, nunca valor: o valor é a cópia em VL_COBRADO.
        // Uma junção que herdasse o filtro StAtiva do catálogo apagaria a receita deste
        // serviço EM SILÊNCIO e quebraria a reconciliação — sem erro, sem log, só um mix que
        // não soma o total.
        using var ctx = CenarioBase(
            nameof(Servico_DESATIVADO_depois_de_faturar_continua_no_mix_com_o_nome_e_o_total_fecha));

        SemearCobranca(ctx, 1, 10, ClinicaA, 60.00m, MeioDoPeriodo, ServicoDesativadoA);
        SemearCobranca(ctx, 2, 11, ClinicaA, 40.00m, MeioDoPeriodo, ServicoConsultaA);

        // A cobrança aconteceu com o serviço ATIVO; ele só sai do catálogo depois. É a ordem
        // real dos fatos, e é a que o teste tem de reproduzir.
        var servico = await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == ServicoDesativadoA);
        servico.StAtiva = false;
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Controle positivo do instrumento: o serviço REALMENTE ficou desativado. Sem isto,
        // um SaveChanges que não persistisse nada faria o teste passar por vácuo.
        (await ctx.ServicosPreco.IgnoreQueryFilters().SingleAsync(s => s.Id == ServicoDesativadoA))
            .StAtiva.Should().BeFalse();

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(100.00m);
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(100.00m,
            "o mix continua fechando com o total mesmo com serviço desativado");

        var balde = resumo.MixPorServico.Single(m => m.IdServicoPreco == ServicoDesativadoA);
        balde.Receita.Should().Be(60.00m);
        balde.NmServico.Should().Be("Banho e tosa (saiu do catálogo)",
            "o RÓTULO vem por FK e não herda o filtro StAtiva do catálogo");

        // F2: literal também aqui. Um `NotBe` contra a própria constante concordaria com ela
        // qualquer que fosse o valor dela.
        balde.NmServico.Should().NotBe("(serviço não encontrado)");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-11 — ISOLAMENTO DE TENANT (a isca EXISTE)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_de_OUTRA_clinica_nao_entra_em_NENHUM_dos_4_KPI()
    {
        // 🔴 A isca do outro tenant é semeada SEMPRE, e no MESMO período: sem linha alheia no
        // banco, "não vazou" seria logicamente incapaz de falhar — o zero viria de a linha
        // não existir para ninguém, não do escopo. Num relatório agregado o custo do erro é
        // maior que numa leitura de linha: o número vazado PARECE plausível.
        using var ctx = CenarioBase(nameof(Cobranca_de_OUTRA_clinica_nao_entra_em_NENHUM_dos_4_KPI));

        // Clínica A: 100,00 no período, 50,00 no anterior.
        SemearCobranca(ctx, 1, 10, ClinicaA, 100.00m, MeioDoPeriodo, ServicoConsultaA);
        SemearCobranca(ctx, 2, 20, ClinicaA, 50.00m, MeioDoAnterior, ServicoConsultaA);

        // Clínica B: valores inconfundíveis, nos MESMOS instantes, em atendimentos próprios.
        SemearCobranca(ctx, 3, 30, ClinicaB, 7777.00m, MeioDoPeriodo, ServicoDaClinicaB);
        SemearCobranca(ctx, 4, 31, ClinicaB, 8888.00m, MeioDoPeriodo, ServicoDaClinicaB);
        SemearCobranca(ctx, 5, 32, ClinicaB, 6666.00m, MeioDoAnterior, ServicoDaClinicaB);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        // KPI 1 — receita bruta.
        resumo.ReceitaBruta.Should().Be(100.00m);
        resumo.NrCobrancas.Should().Be(1);

        // KPI 2 — ticket médio (o denominador também não pode contar atendimento alheio).
        resumo.NrAtendimentosCobrados.Should().Be(1);
        resumo.TicketMedio.Should().Be(100.00m);

        // KPI 3 — mix: nem a receita nem o NOME do serviço do outro tenant aparecem.
        resumo.MixPorServico.Should().HaveCount(1);
        resumo.MixPorServico.Should().OnlyContain(m => m.IdServicoPreco == ServicoConsultaA);
        resumo.MixPorServico.Should().NotContain(m => m.NmServico == "Serviço do outro tenant");
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(100.00m);

        // KPI 4 — comparação com o período anterior.
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(50.00m);
        resumo.VariacaoPercentual.Should().Be(100.00m, "(100 - 50) / 50 * 100");

        // Controle positivo do instrumento: as linhas da clínica B EXISTEM mesmo no banco, e
        // no período. Sem esta asserção o teste seria compatível com um seed que não gravou.
        var alheias = await ctx.Cobrancas.IgnoreQueryFilters()
            .Where(c => c.IdClinica == ClinicaB)
            .ToListAsync();
        alheias.Should().HaveCount(3);
        alheias.Sum(c => c.VlCobrado).Should().Be(23331.00m);
    }

    [Fact]
    public async Task Cobranca_cujo_servico_NAO_EXISTE_nesta_clinica_cai_no_balde_de_CONTINGENCIA()
    {
        // 🔴 F3 da fix wave pós-G2. `RotuloServicoNaoEncontrado` estava documentado como o
        // mecanismo que impede a QUEBRA SILENCIOSA da reconciliação do R-9 — e nenhum teste
        // executava esse caminho: trocá-lo por `RotuloAvulso` não mudava um resultado sequer.
        // Documentação garantindo o que nenhum teste exige é exatamente a regra 5 do ciclo.
        //
        // 🔴 E o cenário é o REAL, não um id inventado: a FK aponta um SERVICO_PRECO que
        // EXISTE no banco, só que na clínica B. `ListarPorIdsNaClinicaAsync` não o traz
        // porque compara a clínica, então o rótulo não resolve. Isso faz deste teste também
        // uma TRAVA DE TENANT no rótulo: se o predicado de clínica daquele repositório cair,
        // o balde passa a exibir "Serviço do outro tenant" — o nome comercial do concorrente
        // dentro do relatório da clínica A — e a asserção abaixo cai.
        using var ctx = CenarioBase(
            nameof(Cobranca_cujo_servico_NAO_EXISTE_nesta_clinica_cai_no_balde_de_CONTINGENCIA));

        SemearCobranca(ctx, 1, 10, ClinicaA, 90.00m, MeioDoPeriodo, ServicoConsultaA);

        // A linha com a FK cruzada. Ela é da clínica A (senão o filtro de tenant das
        // COBRANÇAS já a derrubaria e o teste provaria outra coisa), mas aponta o serviço da
        // clínica B.
        SemearCobranca(ctx, 2, 11, ClinicaA, 60.00m, MeioDoPeriodo, ServicoDaClinicaB);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        // 🔴 O invariante: a receita da linha órfã ENTRA. Descartá-la seria a quebra
        // silenciosa — 90,00 de mix contra 150,00 de receita, sem erro e sem log.
        resumo.ReceitaBruta.Should().Be(150.00m);
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(resumo.ReceitaBruta,
            "o mix RECONCILIA mesmo quando o rótulo de um balde não resolve");
        resumo.MixPorServico.Should().HaveCount(2);

        var contingencia = resumo.MixPorServico.Single(m => m.IdServicoPreco == ServicoDaClinicaB);
        contingencia.Receita.Should().Be(60.00m);
        contingencia.NrCobrancas.Should().Be(1);

        // Literal (F2), não `FinanceiroService.RotuloServicoNaoEncontrado`.
        contingencia.NmServico.Should().Be("(serviço não encontrado)");

        // 🔴 As duas negativas que dizem o que o balde NÃO pode ser:
        // - o nome do serviço alheio seria vazamento cross-tenant no rótulo;
        // - "(avulso)" confundiria FK ausente (legítima, D-2) com FK que não resolve.
        contingencia.NmServico.Should().NotBe("Serviço do outro tenant");
        contingencia.NmServico.Should().NotBe("(avulso)");

        // Controle positivo do instrumento: o serviço da clínica B EXISTE mesmo no banco.
        // Sem esta asserção, o "(serviço não encontrado)" seria compatível com um seed que
        // nunca gravou a linha — e aí o teste não estaria provando o escopo de clínica.
        (await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == ServicoDaClinicaB)).IdClinica.Should().Be(ClinicaB);
    }

    [Fact]
    public async Task Receita_que_DESABA_A_ZERO_devolve_variacao_de_MENOS_100_por_cento()
    {
        // 🔴 F4 da fix wave pós-G2: a 4ª combinação de vazio — período anterior CHEIO,
        // período atual VAZIO. As outras três já tinham trava; esta não, e é provavelmente o
        // número que mais interessa a um gestor ("o faturamento desabou a zero").
        //
        // 🔴 E ela é a única em que o -100 tem de ser CALCULADO em vez de virar null: a
        // guarda de divisão olha a receita ANTERIOR, que aqui é 100,00. Um `if` escrito na
        // ponta errada (receita atual == 0 -> null) devolveria "não medimos" para uma queda
        // total medida — o pior formato possível, porque some do gráfico.
        using var ctx = CenarioBase(nameof(Receita_que_DESABA_A_ZERO_devolve_variacao_de_MENOS_100_por_cento));

        SemearCobranca(ctx, 1, 20, ClinicaA, 100.00m, MeioDoAnterior, ServicoConsultaA);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(0m);
        resumo.NrCobrancas.Should().Be(0);
        resumo.NrAtendimentosCobrados.Should().Be(0);
        resumo.MixPorServico.Should().BeEmpty("o mix é do período ATUAL, que não faturou");

        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(100.00m);
        resumo.NrAtendimentosCobradosPeriodoAnterior.Should().Be(1);

        resumo.VariacaoPercentual.Should().Be(-100.00m, "(0 - 100) / 100 * 100");
        resumo.VariacaoPercentual.Should().NotBeNull(
            "há base de comparação: null aqui seria 'não medimos' para uma queda MEDIDA");

        // O ticket médio, esse SIM é null: zero atendimento não tem ticket. Os dois nulos são
        // decisões diferentes e este caso é o único em que uma vale e a outra não.
        resumo.TicketMedio.Should().BeNull();
    }

    [Fact]
    public async Task Cobranca_SOFT_DELETADA_nao_entra_em_nenhum_KPI()
    {
        using var ctx = CenarioBase(nameof(Cobranca_SOFT_DELETADA_nao_entra_em_nenhum_KPI));

        SemearCobranca(ctx, 1, 10, ClinicaA, 100.00m, MeioDoPeriodo, ServicoConsultaA);

        // Outro atendimento, mesma data: a unica cobranca dele esta INATIVA, entao ele nao
        // pode aparecer nem na receita nem no denominador do ticket.
        SemearCobranca(ctx, 2, 11, ClinicaA, 555.00m, MeioDoPeriodo, ServicoVacinaA, ativa: false);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(100.00m);
        resumo.NrCobrancas.Should().Be(1);
        resumo.NrAtendimentosCobrados.Should().Be(1, "o atendimento 11 só tinha cobrança inativa");
        resumo.MixPorServico.Should().HaveCount(1);
        resumo.MixPorServico.Sum(m => m.Receita).Should().Be(100.00m);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // 🔴 R-12 — ARREDONDAMENTO DECLARADO
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ticket_medio_com_TERCEIRA_casa_forcada_sai_com_2_casas()
    {
        // 3 atendimentos somando 100,00: 100/3 = 33,3333... -> 33,33.
        using var ctx = CenarioBase(nameof(Ticket_medio_com_TERCEIRA_casa_forcada_sai_com_2_casas));
        SemearCobranca(ctx, 1, 10, ClinicaA, 33.33m, MeioDoPeriodo);
        SemearCobranca(ctx, 2, 11, ClinicaA, 33.33m, MeioDoPeriodo);
        SemearCobranca(ctx, 3, 12, ClinicaA, 33.34m, MeioDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        // 🔴 A soma é EXATA e NÃO arredondada — cada parcela já é NUMBER(10,2).
        resumo.ReceitaBruta.Should().Be(100.00m);
        resumo.NrAtendimentosCobrados.Should().Be(3);
        resumo.TicketMedio.Should().Be(33.33m);
    }

    [Fact]
    public async Task Ticket_medio_no_MEIO_do_intervalo_arredonda_para_LONGE_do_zero()
    {
        // 🔴 O caso que distingue AwayFromZero de ToEven, que é o default do .NET:
        // 0,25 / 2 = 0,125. AwayFromZero -> 0,13. ToEven (bancário) -> 0,12.
        // Sem este teste, `Math.Round(x, 2)` sem o modo passaria despercebido.
        using var ctx = CenarioBase(nameof(Ticket_medio_no_MEIO_do_intervalo_arredonda_para_LONGE_do_zero));
        SemearCobranca(ctx, 1, 10, ClinicaA, 0.13m, MeioDoPeriodo);
        SemearCobranca(ctx, 2, 11, ClinicaA, 0.12m, MeioDoPeriodo);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(0.25m);
        resumo.NrAtendimentosCobrados.Should().Be(2);
        resumo.TicketMedio.Should().Be(0.13m, "MidpointRounding.AwayFromZero");
        resumo.TicketMedio.Should().NotBe(0.12m, "0,12 seria o ToEven padrão do .NET");
    }

    [Fact]
    public async Task Variacao_percentual_no_MEIO_do_intervalo_arredonda_para_LONGE_do_zero()
    {
        // 801 sobre 800 = +0,125%. AwayFromZero -> 0,13. ToEven -> 0,12.
        using var ctx = CenarioBase(nameof(Variacao_percentual_no_MEIO_do_intervalo_arredonda_para_LONGE_do_zero));
        SemearCobranca(ctx, 1, 10, ClinicaA, 801.00m, MeioDoPeriodo);
        SemearCobranca(ctx, 2, 20, ClinicaA, 800.00m, MeioDoAnterior);

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.VariacaoPercentual.Should().Be(0.13m, "MidpointRounding.AwayFromZero");
        resumo.VariacaoPercentual.Should().NotBe(0.12m, "0,12 seria o ToEven padrão do .NET");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Estado vazio
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Periodo_sem_nenhuma_cobranca_devolve_estado_vazio_DECLARADO()
    {
        // Não é 404 e não é 500: é um período que existe e não teve faturamento. O que o
        // distingue de "não sabemos" são os nulos das duas divisões.
        using var ctx = CenarioBase(nameof(Periodo_sem_nenhuma_cobranca_devolve_estado_vazio_DECLARADO));

        var resumo = await CriarService(ctx, ClinicaA).ObterResumoAsync(De, Ate);

        resumo.ReceitaBruta.Should().Be(0m);
        resumo.NrCobrancas.Should().Be(0);
        resumo.NrAtendimentosCobrados.Should().Be(0);
        resumo.TicketMedio.Should().BeNull();
        resumo.ReceitaBrutaPeriodoAnterior.Should().Be(0m);
        resumo.VariacaoPercentual.Should().BeNull();
        resumo.MixPorServico.Should().BeEmpty();
        resumo.Periodo.De.Should().Be(De);
        resumo.PeriodoAnterior.Ate.Should().Be(AnteriorAte);
    }
}
