namespace Kura.Application.DTOs.Financeiro;

/// <summary>
/// FD-11 — os 4 KPI financeiros da clínica do JWT no período, <b>numa resposta só</b>.
///
/// <para>
/// 🔴 <b>Um endpoint, não quatro, e o motivo é de correção e não de economia.</b> Quatro
/// chamadas sobre "o mesmo" período são quatro leituras em quatro instantes: uma cobrança
/// lançada entre a segunda e a terceira faz os cards do app <b>discordarem entre si</b> —
/// receita de um instante, ticket de outro, mix de um terceiro, e nenhum deles errado
/// isoladamente. Aqui os quatro saem da <b>mesma</b> lista de cobranças.
/// </para>
///
/// <para>
/// ⛔ <b>Escopo negativo declarado (D-1/D-6):</b> não há imposto, repasse nem margem — o
/// campo se chama <see cref="ReceitaBruta"/> com essas palavras justamente para que ninguém
/// o leia como lucro. Não há status de pagamento, gateway, projeção ou previsão; não há mix
/// por veterinário nem estoque.
/// </para>
/// </summary>
public sealed class ResumoFinanceiroResponseDto
{
    /// <summary>
    /// O período que o servidor <b>de fato usou</b>, já resolvido em instantes UTC. Ver
    /// <see cref="PeriodoResumoDto"/> — devolvê-lo é o que permite ao app conferir a borda
    /// em vez de confiar nela.
    /// </summary>
    public required PeriodoResumoDto Periodo { get; init; }

    /// <summary>
    /// Período de comparação: <b>mesma duração, imediatamente antes</b>, sem sobrepor nem um
    /// dia. Devolvido pelo mesmo motivo do <see cref="Periodo"/> — o app não precisa
    /// reimplementar a aritmética para saber contra o que está comparando.
    /// </summary>
    public required PeriodoResumoDto PeriodoAnterior { get; init; }

    /// <summary>
    /// KPI 1 — <b>receita bruta</b>: soma de <c>VL_COBRADO</c> das cobranças ATIVAS da
    /// clínica no período.
    ///
    /// <para>🔴 <b>Soma exata, NÃO arredondada.</b> Cada parcela já é <c>NUMBER(10,2)</c>;
    /// arredondar a soma de valores que já têm 2 casas só poderia introduzir erro, nunca
    /// removê-lo. O arredondamento existe onde há divisão — <see cref="TicketMedio"/> e
    /// <see cref="VariacaoPercentual"/>.</para>
    ///
    /// <para>⚠️ <b>Bruta</b> quer dizer bruta: sem imposto, sem repasse ao veterinário, sem
    /// custo. Não é margem nem lucro (D-6).</para>
    /// </summary>
    public required decimal ReceitaBruta { get; init; }

    /// <summary>Quantidade de <b>lançamentos</b> ativos somados. Não é o denominador do ticket.</summary>
    public required int NrCobrancas { get; init; }

    /// <summary>
    /// Denominador do <see cref="TicketMedio"/>: quantidade de <b>atendimentos</b> distintos
    /// (<c>ID_EVENTO_CLINICO</c> distinto) que tiveram cobrança no período.
    ///
    /// <para>🔴 <b>Exposto de propósito.</b> Ticket médio sem o denominador é um número que
    /// não dá para auditar; com ele, o gestor reconcilia
    /// <c>ReceitaBruta / NrAtendimentosCobrados</c> na mão.</para>
    /// </summary>
    public required int NrAtendimentosCobrados { get; init; }

    /// <summary>
    /// KPI 2 — <b>ticket médio</b>: <see cref="ReceitaBruta"/> ÷
    /// <see cref="NrAtendimentosCobrados"/>, arredondado a 2 casas com
    /// <c>MidpointRounding.AwayFromZero</c>.
    ///
    /// <para>🔴 <b>Divide por ATENDIMENTO, não por lançamento.</b> Um atendimento com
    /// consulta + vacina + medicamento são 3 cobranças e <b>um</b> ticket. Dividir por
    /// cobrança responderia "valor médio do item vendido", que é outra pergunta e é sempre
    /// menor — o gestor leria queda de ticket onde houve aumento de itens por atendimento.</para>
    ///
    /// <para>🔴 <b><c>null</c> quando <see cref="NrAtendimentosCobrados"/> é zero — nunca
    /// <c>0</c>.</b> Zero atendimentos não tem ticket médio; devolver <c>0</c> afirmaria que
    /// o atendimento médio valeu R$ 0,00, o que é falso, e o ponto sumiria no gráfico junto
    /// de um mês ruim de verdade. Zero para dizer "não medimos" é o defeito que este ciclo
    /// existe para matar.</para>
    /// </summary>
    public decimal? TicketMedio { get; init; }

    /// <summary>
    /// Receita bruta do <see cref="PeriodoAnterior"/>, <b>crua</b>.
    ///
    /// <para>🔴 <b>Devolvida mesmo quando <see cref="VariacaoPercentual"/> é <c>null</c></b>,
    /// e é aí que ela mais importa: com o anterior em zero não existe porcentagem, mas existe
    /// uma frase honesta a dizer (de R$ 0,00 para R$ 4.200,00). Sem o número cru o app só
    /// teria um traço.</para>
    /// </summary>
    public required decimal ReceitaBrutaPeriodoAnterior { get; init; }

    /// <summary>Atendimentos cobrados no <see cref="PeriodoAnterior"/>, cru, pelo mesmo motivo.</summary>
    public required int NrAtendimentosCobradosPeriodoAnterior { get; init; }

    /// <summary>
    /// KPI 4 — <b>comparação com o período anterior</b>, em porcentagem:
    /// <c>(receita − receitaAnterior) / receitaAnterior × 100</c>, arredondada a 2 casas com
    /// <c>MidpointRounding.AwayFromZero</c>.
    ///
    /// <para>🔴 <b><c>null</c> quando a receita do período anterior é <c>0</c></b> — crescer
    /// do zero não tem porcentagem. E a divisão não tratada não daria um número estranho:
    /// <c>decimal</c> lança <c>DivideByZeroException</c> (não devolve <c>Infinity</c> como
    /// <c>double</c>), então o caso omitido seria <c>500</c>. <c>0</c> aqui mentiria dizendo
    /// "estável".</para>
    /// </summary>
    public decimal? VariacaoPercentual { get; init; }

    /// <summary>
    /// KPI 3 — <b>mix por serviço</b>: a receita do período repartida por
    /// <c>ID_SERVICO_PRECO</c>, maior primeiro.
    ///
    /// <para>🔴 <b>O mix RECONCILIA: soma das receitas dos baldes ==
    /// <see cref="ReceitaBruta"/>, exato.</b> É o invariante da task, e é o que dá ao gestor o
    /// direito de somar os pedaços. As duas formas de quebrá-lo estão travadas por teste: o
    /// <b>balde avulso</b> (<c>ID_SERVICO_PRECO</c> nulo, lançamento legítimo pela D-2) e o
    /// serviço <b>desativado</b> — ver <see cref="MixPorServicoDto"/>.</para>
    /// </summary>
    public required IReadOnlyList<MixPorServicoDto> MixPorServico { get; init; }
}

/// <summary>
/// FD-11 — o período tal como o servidor o resolveu, devolvido na resposta.
///
/// <para>
/// 🔴 <b>Devolver o período usado é parte do contrato, não enfeite.</b> A conversão de duas
/// datas inclusivas para um intervalo de instantes é justamente onde a receita do último dia
/// some; com <see cref="InicioUtc"/>/<see cref="FimExclusivoUtc"/> na resposta, o app (e o
/// revisor) conferem a borda em vez de acreditar nela.
/// </para>
/// </summary>
public sealed class PeriodoResumoDto
{
    /// <summary>Primeiro dia, inclusivo — como pedido (ou como derivado, no período anterior).</summary>
    public required DateOnly De { get; init; }

    /// <summary>Último dia, <b>inclusivo</b>.</summary>
    public required DateOnly Ate { get; init; }

    /// <summary>
    /// Instante inicial usado no filtro: <c>De</c> às <c>00:00:00</c>, <b>inclusivo</b>.
    ///
    /// <para>🔴 <b>UTC, sem conversão de fuso</b> — <c>COBRANCA.DT_COBRANCA</c> é gravada
    /// como <c>DateTime.UtcNow</c> (FD-10), então comparar em UTC é comparar com o que está
    /// no banco. A convenção de fuso de exibição do projeto é item em aberto e <b>não nasce
    /// aqui</b>: uma segunda convenção inventada neste endpoint conflitaria com a que for
    /// decidida depois. Consequência declarada: para uma clínica em <c>America/Sao_Paulo</c>,
    /// o "dia" deste relatório é o dia UTC — as 3 primeiras horas de cada dia local caem no
    /// dia anterior do relatório.</para>
    /// </summary>
    public required DateTime InicioUtc { get; init; }

    /// <summary>
    /// Instante final usado no filtro: <c>Ate + 1 dia</c> às <c>00:00:00</c>,
    /// <b>EXCLUSIVO</b>.
    ///
    /// <para>🔴 <b>Semiaberto é o que faz o último dia contar inteiro.</b> Uma cobrança de
    /// <c>Ate 23:59:59</c> é menor que <see cref="FimExclusivoUtc"/> e entra; um filtro
    /// <c>&lt;= Ate 00:00</c> a descartaria junto com as outras 23h59 daquele dia.</para>
    /// </summary>
    public required DateTime FimExclusivoUtc { get; init; }
}

/// <summary>
/// FD-11 — um balde do mix por serviço.
///
/// <para>
/// 🔴 <b>O nome do serviço é RÓTULO, nunca valor.</b> O valor é a cópia gravada em
/// <c>COBRANCA.VL_COBRADO</c> no lançamento (FD-10); <c>SERVICO_PRECO</c> entra aqui só para
/// dizer como se chama o balde. Consequências travadas por teste:
/// <list type="bullet">
///   <item><description><b>Serviço DESATIVADO continua no mix, com o nome dele.</b> A receita
///   aconteceu; desativar o item do catálogo depois não a desfaz. Uma junção que herdasse o
///   filtro <c>StAtiva</c> do catálogo apagaria essa receita <b>em silêncio</b> e quebraria a
///   reconciliação do mix — sem erro, sem log, só um total que não fecha.</description></item>
///   <item><description><b>Lançamento avulso tem balde PRÓPRIO</b>
///   (<see cref="IdServicoPreco"/> nulo), nunca é descartado. Valor sem serviço tabelado é
///   lançamento legítimo (D-2) e é receita como qualquer outra.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class MixPorServicoDto
{
    /// <summary>
    /// Serviço de origem, ou <c>null</c> para o balde dos lançamentos <b>avulsos</b>.
    /// </summary>
    public long? IdServicoPreco { get; init; }

    /// <summary>
    /// Rótulo do balde: o nome do serviço, ou o rótulo do balde avulso quando
    /// <see cref="IdServicoPreco"/> é nulo. Ver <c>FinanceiroService.RotuloAvulso</c> e
    /// <c>FinanceiroService.RotuloServicoNaoEncontrado</c>.
    /// </summary>
    public required string NmServico { get; init; }

    /// <summary>Soma de <c>VL_COBRADO</c> das cobranças deste balde. Exata, não arredondada.</summary>
    public required decimal Receita { get; init; }

    /// <summary>Quantidade de lançamentos no balde.</summary>
    public required int NrCobrancas { get; init; }
}
