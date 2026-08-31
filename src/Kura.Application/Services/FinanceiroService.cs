namespace Kura.Application.Services;

using Kura.Application.DTOs.Financeiro;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

/// <summary>
/// FD-11 (ciclo FIN) — os 4 KPI financeiros da clínica: <b>receita bruta</b>, <b>ticket
/// médio</b>, <b>mix por serviço</b> e <b>comparação com o período anterior</b>. É aqui que
/// as linhas que a FD-10 passou a escrever viram a única tela que o gestor abre.
///
/// <para>
/// 🔴 <b>A BORDA SUPERIOR É A ARMADILHA CENTRAL DA TASK.</b> O gestor pede duas datas
/// <b>inclusivas</b> ("de 01/08 até 31/08"). O filtro ingênuo <c>DtCobranca &lt;= ate</c>
/// compara com <c>31/08 00:00:00</c> e <b>descarta o dia 31 inteiro</b> — 23h59 de receita
/// real somem do relatório sem erro, sem log e sem nada que pareça errado no número
/// resultante. A conversão feita aqui é para um intervalo <b>semiaberto</b> de instantes,
/// <c>[de 00:00, ate+1d 00:00)</c>, e ela está travada por teste com <b>datas literais</b>
/// (uma cobrança às <c>23:59</c> do último dia CONTA).
/// </para>
///
/// <para>
/// 🔴 <b>FUSO: UTC, declarado, sem conversão.</b> <c>COBRANCA.DT_COBRANCA</c> é gravada como
/// <c>DateTime.UtcNow</c> (FD-10), então o filtro compara em UTC — que é o que está no banco.
/// <b>Nenhuma conversão para horário local é inventada aqui</b>: a convenção de fuso de
/// exibição do projeto é item em aberto, e uma segunda convenção nascida dentro deste
/// endpoint conflitaria com a que for decidida depois. A limitação é <b>declarada</b> (o
/// "dia" deste relatório é o dia UTC) e a resposta devolve o intervalo que de fato usou, em
/// <c>Periodo.InicioUtc</c>/<c>Periodo.FimExclusivoUtc</c>, para que o app confira em vez de
/// acreditar.
/// </para>
///
/// <para>
/// 🔴 <b>A CHAVE DE AGREGAÇÃO É <c>DT_COBRANCA</c> — a data do FATO, nunca
/// <c>DT_CRIACAO</c>.</b> São coisas diferentes e divergem no caso real que a FD-10 aceita de
/// propósito: o fechamento do dia anterior, lançado na manhã seguinte, tem <c>DT_COBRANCA</c>
/// de ontem e <c>DT_CRIACAO</c> de hoje. Agregar pela criação jogaria essa receita no dia
/// errado. ⚠️ <b>E este é o controle durável que a FD-10 deve à FD-11</b> (revisão G2 da
/// FD-10, commit <c>79878fa</c>): <c>CobrancaCreateValidator.ToleranciaFutura</c> aceita
/// <c>DtCobranca</c> até <b>+1 dia no futuro</b> para absorver fuso de cliente, e a
/// consequência declarada é que uma cobrança lançada em 31/01 no limite da tolerância cai no
/// balde de <b>fevereiro</b>. Isso é comportamento <b>declarado</b>, não bug a consertar aqui
/// — apertar a tolerância só estreitaria a janela sem eliminar a classe, e quebraria o
/// cliente que serializa <c>DateTime</c> sem offset. O que a FD-11 deve em troca é
/// <b>travar a chave por teste</b>, com um cenário em que as duas datas DIVERGEM
/// (<c>FinanceiroServiceTests</c>): sem ele, alguém "otimiza" o filtro para <c>DtCriacao</c>
/// e a suíte inteira fica verde, porque nos testes as duas datas costumam coincidir.
/// </para>
///
/// <para>
/// 🔴 <b>Escopo de tenant é comparação EXPLÍCITA, por argumento</b> — mesma disciplina de
/// <see cref="CobrancaService"/> e da FD-09. Nenhuma consulta daqui depende do query filter,
/// que <b>desliga inteiro</b> (não nega) quando não há JWT no contexto. Num relatório
/// agregado o custo do erro é maior que numa leitura de linha: um filtro desligado devolve o
/// faturamento inteiro do concorrente numa chamada, e o número resultante <b>parece
/// perfeitamente plausível</b>.
/// </para>
///
/// <para>
/// <b>Agregação em MEMÓRIA, sobre uma única consulta de faixa</b> — decisão de risco, não de
/// preguiça. Este repositório não tem um único teste que toque Oracle, e <c>GroupBy</c> com
/// chave <b>nula</b> (<c>ID_SERVICO_PRECO</c> é nullable pela D-2) traduzido pelo provider
/// Oracle é território não provado aqui; o modo de falha desta casa é <i>verde no InMemory,
/// 500 em produção</i>. O índice <c>IDX_COBRANCA_CLINICA_DATA</c> da V18 serve exatamente a
/// consulta de faixa que fazemos, e o volume mensal de uma clínica é da ordem de centenas de
/// linhas. Se alguém discordar, que discorde com <b>medição</b> — o argumento fica registrado
/// para a FD-12.
/// </para>
///
/// <para>
/// ⛔ <b>Escopo negativo (D-1/D-6):</b> sem imposto, repasse ou margem — o campo se chama
/// <c>ReceitaBruta</c> com essas palavras. Sem gateway, status de pagamento, projeção ou
/// previsão. Sem mix por veterinário, sem estoque, sem exportação.
/// </para>
/// </summary>
public sealed class FinanceiroService : IFinanceiroService
{
    /// <summary>
    /// Rótulo do balde dos lançamentos sem serviço tabelado (<c>ID_SERVICO_PRECO</c> nulo,
    /// legítimo pela D-2). Constante para que o teste de reconciliação do mix possa nomeá-lo
    /// sem repetir literal.
    /// </summary>
    public const string RotuloAvulso = "(avulso)";

    /// <summary>
    /// Rótulo de contingência: cobrança que aponta um <c>ID_SERVICO_PRECO</c> para o qual não
    /// há linha correspondente <b>nesta clínica</b>.
    ///
    /// <para>⚠️ Não é caso esperado — a FD-10 só grava a FK depois de provar que o serviço é
    /// desta clínica. Ele existe porque a alternativa seria <b>descartar</b> a linha do mix e
    /// quebrar a reconciliação em silêncio, que é exatamente o defeito que esta task
    /// persegue. Melhor um balde com nome feio e o total fechando do que receita sumindo.</para>
    /// </summary>
    public const string RotuloServicoNaoEncontrado = "(serviço não encontrado)";

    private readonly ICobrancaRepository _cobrancaRepository;
    private readonly IServicoPrecoRepository _servicoPrecoRepository;
    private readonly IClinicaContext _clinicaContext;

    public FinanceiroService(
        ICobrancaRepository cobrancaRepository,
        IServicoPrecoRepository servicoPrecoRepository,
        IClinicaContext clinicaContext)
    {
        _cobrancaRepository = cobrancaRepository;
        _servicoPrecoRepository = servicoPrecoRepository;
        _clinicaContext = clinicaContext;
    }

    public async Task<ResumoFinanceiroResponseDto> ObterResumoAsync(DateOnly de, DateOnly ate)
    {
        var idClinica = _clinicaContext.IdClinica;

        var periodo = PeriodoResumo.Criar(de, ate);
        var anterior = periodo.Anterior();

        // 🔴 UMA consulta de faixa cobrindo os DOIS períodos (eles são contíguos por
        // construção: anterior.FimExclusivoUtc == periodo.InicioUtc). A partição acontece
        // em memória, logo abaixo.
        var cobrancas = await _cobrancaRepository.ListarDaClinicaNoPeriodoAsync(
            idClinica, anterior.InicioUtc, periodo.FimExclusivoUtc);

        var doPeriodo = cobrancas.Where(c => c.DtCobranca >= periodo.InicioUtc).ToList();
        var doAnterior = cobrancas.Where(c => c.DtCobranca < periodo.InicioUtc).ToList();

        var receita = SomarReceita(doPeriodo);
        var receitaAnterior = SomarReceita(doAnterior);
        var atendimentos = ContarAtendimentos(doPeriodo);

        return new ResumoFinanceiroResponseDto
        {
            Periodo = periodo.ToDto(),
            PeriodoAnterior = anterior.ToDto(),

            // 🔴 SOMA EXATA, sem arredondar: cada parcela já é NUMBER(10,2).
            ReceitaBruta = receita,
            NrCobrancas = doPeriodo.Count,
            NrAtendimentosCobrados = atendimentos,
            TicketMedio = CalcularTicketMedio(receita, atendimentos),

            ReceitaBrutaPeriodoAnterior = receitaAnterior,
            NrAtendimentosCobradosPeriodoAnterior = ContarAtendimentos(doAnterior),
            VariacaoPercentual = CalcularVariacaoPercentual(receita, receitaAnterior),

            MixPorServico = await MontarMixAsync(doPeriodo, idClinica),
        };
    }

    private static decimal SomarReceita(IEnumerable<Cobranca> cobrancas) =>
        cobrancas.Sum(c => c.VlCobrado);

    /// <summary>
    /// 🔴 <b>ATENDIMENTOS distintos, não lançamentos.</b> Um atendimento com consulta + vacina
    /// + medicamento são 3 cobranças e <b>um</b> ticket. Ver
    /// <c>ResumoFinanceiroResponseDto.TicketMedio</c>.
    /// </summary>
    private static int ContarAtendimentos(IEnumerable<Cobranca> cobrancas) =>
        cobrancas.Select(c => c.IdEventoClinico).Distinct().Count();

    /// <summary>
    /// 🔴 <b>Divisão nº 1 das DUAS que este endpoint faz.</b> <c>null</c> quando não houve
    /// atendimento cobrado — nunca <c>0</c>, nunca <c>NaN</c>, nunca <c>500</c>.
    ///
    /// <para>Arredondamento <b>declarado</b>: 2 casas, <c>MidpointRounding.AwayFromZero</c>.
    /// O default do .NET é <c>ToEven</c> (arredondamento bancário), que faria
    /// <c>R$ 0,125</c> virar <c>R$ 0,12</c> — defensável em estatística, surpreendente num
    /// relatório financeiro que o gestor confere na calculadora.</para>
    /// </summary>
    private static decimal? CalcularTicketMedio(decimal receita, int atendimentos) =>
        atendimentos == 0
            ? null
            : Math.Round(receita / atendimentos, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 🔴 <b>Divisão nº 2 — a que o enunciado da task não nomeia.</b> <c>null</c> quando a
    /// receita do período anterior é <c>0</c>: crescer do zero não tem porcentagem.
    ///
    /// <para>⚠️ E o caso omitido não daria um número esquisito: <c>decimal</c> lança
    /// <c>DivideByZeroException</c> (ao contrário de <c>double</c>, que devolveria
    /// <c>Infinity</c>), então "esquecer" esta guarda produziria <c>500</c> no primeiro mês de
    /// uso de qualquer clínica — o exato momento em que a tela é aberta pela primeira
    /// vez.</para>
    ///
    /// <para>O par cru (<c>receita</c>, <c>receitaAnterior</c>) vai na resposta de qualquer
    /// jeito, para que o app tenha algo honesto a dizer quando a porcentagem é nula.</para>
    /// </summary>
    private static decimal? CalcularVariacaoPercentual(decimal receita, decimal receitaAnterior) =>
        receitaAnterior == 0m
            ? null
            : Math.Round(
                (receita - receitaAnterior) / receitaAnterior * 100m,
                2,
                MidpointRounding.AwayFromZero);

    /// <summary>
    /// 🔴 <b>O MIX RECONCILIA — é o invariante da task.</b> Toda cobrança do período cai em
    /// exatamente um balde, então a soma dos baldes é <b>igual</b> à receita bruta, exata.
    ///
    /// <para>As duas formas de quebrar isso estão fechadas aqui, e as duas quebram em
    /// silêncio se alguém as reabrir:
    /// <list type="number">
    ///   <item><description><b>Avulso</b> (<c>IdServicoPreco</c> nulo) é balde próprio, com
    ///   rótulo declarado — não é descartado nem somado ao balde de outro serviço.</description></item>
    ///   <item><description><b>Serviço desativado</b> continua no mix, com o nome dele: o
    ///   rótulo vem de <c>ListarPorIdsNaClinicaAsync</c>, que <b>não</b> filtra
    ///   <c>StAtiva</c>. Uma junção que herdasse esse filtro apagaria a receita do serviço
    ///   desativado.</description></item>
    /// </list>
    /// </para>
    ///
    /// <para><b>O agrupamento acontece em LINQ-to-Objects</b> — a chave nula do avulso é
    /// tratada pelo <c>EqualityComparer</c> do CLR, e não pela semântica de <c>NULL</c> do
    /// SQL, que agruparia diferente (ou nem traduziria). Ver a documentação da classe.</para>
    /// </summary>
    private async Task<IReadOnlyList<MixPorServicoDto>> MontarMixAsync(
        IReadOnlyList<Cobranca> cobrancas, long idClinica)
    {
        if (cobrancas.Count == 0)
            return [];

        var idsDeServico = cobrancas
            .Where(c => c.IdServicoPreco.HasValue)
            .Select(c => c.IdServicoPreco!.Value)
            .Distinct()
            .ToArray();

        var servicos = await _servicoPrecoRepository.ListarPorIdsNaClinicaAsync(
            idsDeServico, idClinica);

        var nomePorId = servicos.ToDictionary(s => s.Id, s => s.NmServico);

        return cobrancas
            .GroupBy(c => c.IdServicoPreco)
            .Select(g => new MixPorServicoDto
            {
                IdServicoPreco = g.Key,
                NmServico = ResolverRotulo(g.Key, nomePorId),
                Receita = g.Sum(c => c.VlCobrado),
                NrCobrancas = g.Count(),
            })
            .OrderByDescending(m => m.Receita)
            // Desempate estável para que a ordem não dependa da ordem de chegada das linhas:
            // o balde avulso (id nulo) vai por último entre empatados.
            .ThenBy(m => m.IdServicoPreco ?? long.MaxValue)
            .ToList();
    }

    private static string ResolverRotulo(long? idServicoPreco, IReadOnlyDictionary<long, string> nomePorId)
    {
        if (idServicoPreco is null)
            return RotuloAvulso;

        return nomePorId.TryGetValue(idServicoPreco.Value, out var nome)
            ? nome
            : RotuloServicoNaoEncontrado;
    }

    /// <summary>
    /// Período resolvido: as duas datas <b>inclusivas</b> do gestor mais o intervalo
    /// <b>semiaberto</b> de instantes UTC que o filtro de fato usa.
    ///
    /// <para>🔴 A aritmética mora num tipo próprio, e não espalhada pelo service, porque ela é
    /// o ponto onde os dois erros silenciosos desta task nascem: perder o último dia (borda
    /// superior) e sobrepor o período de comparação (borda inferior do anterior).</para>
    /// </summary>
    private readonly record struct PeriodoResumo(
        DateOnly De, DateOnly Ate, DateTime InicioUtc, DateTime FimExclusivoUtc)
    {
        /// <summary>
        /// 🔴 <b>PRECONDIÇÃO, e ela é do <c>ResumoFinanceiroQueryValidator</c>:</b> o par
        /// <c>(de, ate)</c> tem de ser <b>computável</b> — <c>ate</c> precisa ter dia
        /// seguinte no calendário e <c>de</c> precisa ter <c>duração</c> dias de folga antes
        /// dele. <c>DateOnly.AddDays</c> <b>lança</b> fora de
        /// <c>[0001-01-01, 9999-12-31]</c> em vez de saturar, então violar a precondição aqui
        /// é <c>500</c>, não número errado.
        ///
        /// <para>⚠️ A guarda mora no validator DE PROPÓSITO, e não aqui: neste ponto o erro
        /// só teria como virar exceção, e o que o gestor precisa é de um <c>400</c> com
        /// mensagem acionável. Saturar em vez de recusar seria pior que as duas coisas —
        /// devolveria um período <b>diferente do pedido</b>, com números plausíveis. Provado
        /// por rota HTTP em <c>FinanceiroResumoHttpTests</c>
        /// (<c>Periodo_NAO_COMPUTAVEL_devolve_400_e_nunca_5xx</c>, com controle positivo),
        /// não por leitura.</para>
        /// </summary>
        public static PeriodoResumo Criar(DateOnly de, DateOnly ate) => new(
            de,
            ate,
            MeiaNoiteUtc(de),

            // 🔴 +1 dia, EXCLUSIVO. É o que faz o último dia contar inteiro: uma cobrança de
            // `ate 23:59:59` é estritamente menor que este instante. `<= MeiaNoiteUtc(ate)`
            // descartaria 23h59 de receita real.
            MeiaNoiteUtc(ate.AddDays(1)));

        /// <summary>
        /// Quantos DIAS o período cobre. Inclusivo dos dois lados, então
        /// <c>de == ate</c> é <b>1</b> dia, não zero.
        /// </summary>
        public int DuracaoEmDias => Ate.DayNumber - De.DayNumber + 1;

        /// <summary>
        /// 🔴 <b>Mesma duração, imediatamente antes, sem sobrepor nem um dia.</b> O fim
        /// exclusivo do anterior é <b>exatamente</b> o início do atual — contíguos, disjuntos.
        ///
        /// <para>O erro clássico aqui é <c>De.AddDays(-DuracaoEmDias)</c> até
        /// <c>De</c> (inclusivo), que faria o primeiro dia do período atual ser contado
        /// <b>duas vezes</b>: uma na receita e outra na base de comparação, deflacionando a
        /// variação percentual. O último dia do anterior é <c>De.AddDays(-1)</c>.</para>
        /// </summary>
        public PeriodoResumo Anterior()
        {
            var duracao = DuracaoEmDias;
            return Criar(De.AddDays(-duracao), De.AddDays(-1));
        }

        public PeriodoResumoDto ToDto() => new()
        {
            De = De,
            Ate = Ate,
            InicioUtc = InicioUtc,
            FimExclusivoUtc = FimExclusivoUtc,
        };

        /// <summary>
        /// 🔴 <c>DateTimeKind.Utc</c> explícito. <c>DateOnly.ToDateTime</c> devolve
        /// <c>Unspecified</c>; deixar assim faria a resposta serializar sem o sufixo
        /// <c>Z</c> e o app leria o instante como horário local dele — o mesmo tipo de
        /// ambiguidade de fuso que esta task se recusa a introduzir.
        /// </summary>
        private static DateTime MeiaNoiteUtc(DateOnly dia) =>
            DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }
}
