namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Financeiro;

/// <summary>
/// FD-11 — contrato de entrada do resumo financeiro.
///
/// <para>
/// 🔴 <b><c>de</c> e <c>ate</c> são OBRIGATÓRIOS, sem default de servidor.</b> A tentação é
/// "sem parâmetro, devolve o mês corrente" — e ela é ruim por dois motivos medidos neste
/// projeto: (1) o mês corrente do <b>servidor</b> é UTC, e o do gestor não é, então o default
/// seria uma segunda convenção de fuso nascendo escondida num default; (2) um cliente com bug
/// que deixasse de mandar o período receberia <b>200 com números plausíveis de outro
/// período</b> em vez de <c>400</c> — a classe de defeito que este ciclo persegue é
/// exatamente essa, o número errado que parece certo.
/// </para>
///
/// <para>
/// <b>O que cada caminho de <c>400</c> cobre.</b> Parâmetro <b>ausente</b> e <b>mal formado</b>
/// são pegos em lugares diferentes, e isso é do framework, não desta classe: formato inválido
/// (<c>?de=ontem</c>, <c>?de=2026-13-45</c>) morre no <b>model binding</b> e vira <c>400</c>
/// pelo <c>[ApiController]</c> antes de este validator rodar; ausência e <c>de &gt; ate</c>
/// são regras <b>daqui</b>. Os três estão medidos em <c>FinanceiroResumoHttpTests</c> — se um
/// dia o binder mudar de comportamento, o teste cai, não a documentação.
/// </para>
///
/// <para>
/// ⚠️ <b><c>de == ate</c> é VÁLIDO de propósito</b>: relatório de um único dia é o caso mais
/// comum do fechamento diário. A regra é <c>de &lt;= ate</c>, não <c>de &lt; ate</c>.
/// </para>
///
/// <para>
/// 🔴 <b>F1 da fix wave pós-G2 — ESTE VALIDATOR É A ÚNICA COISA ENTRE O ENDPOINT E UM
/// <c>500</c>.</b> A revisão G2 mediu <c>?de=9999-12-01&amp;ate=9999-12-31</c> devolvendo
/// <b><c>500</c></b> com <c>ArgumentOutOfRangeException</c>, e o mesmo por baixo com
/// <c>0001-01-01</c>. A causa não é do relatório: <c>DateOnly.AddDays</c> <b>lança</b> fora de
/// <c>[0001-01-01, 9999-12-31]</c> em vez de saturar, e o service precisa de dois dias que
/// podem não existir — o <b>dia seguinte</b> a <c>ate</c> (o limite exclusivo que faz o último
/// dia contar inteiro) e o <b>início do período anterior</b>, <c>de − duração</c>. E
/// <c>?ate=9999-12-31</c> não é hipótese de laboratório: é a forma canônica de dizer "tudo até
/// o fim", e um seletor de data com campo vazio ou <c>max</c> a produz sozinho.
/// </para>
///
/// <para>
/// <b>A invariante que as regras abaixo estabelecem:</b> <i>nenhum valor dentro do domínio
/// aceito de <see cref="DateOnly"/> pode produzir <c>5xx</c> neste endpoint</i>. Todo período
/// não computável morre em <c>400</c> <b>aqui</b>, com mensagem acionável, e não em <c>500</c>
/// lá dentro.
/// </para>
///
/// <para>
/// 🔴 <b>São DUAS regras, por DOIS motivos, e colapsá-las numa só reabre o defeito.</b> A
/// <b>borda de calendário</b> é a correção do defeito; o <b>teto de duração</b> é guarda de
/// <b>volume</b> (a agregação do resumo é feita em memória). Um teto de duração sozinho
/// <b>não</b> fecha o F1: <c>9999-12-01 → 9999-12-31</c> são <b>31 dias</b>, passam por
/// qualquer teto, e continuam estourando no <c>ate + 1 dia</c>.
/// </para>
///
/// <para>
/// ⚠️ <b>Toda comparação de duração aqui é aritmética de <c>DayNumber</c></b>
/// (<c>Ate.DayNumber − De.DayNumber</c>), <b>nunca</b> <c>de.AddDays(teto)</c> — que é
/// exatamente a chamada que lança, e escrevê-la aqui reintroduziria o bug dentro do próprio
/// fix. Subtração de <c>int</c> não estoura no domínio de <c>DateOnly</c>
/// (<c>0 … 3_652_058</c>).
/// </para>
/// </summary>
public sealed class ResumoFinanceiroQueryValidator : AbstractValidator<ResumoFinanceiroQueryDto>
{
    public const string MensagemDeObrigatorio =
        "Informe 'de' no formato YYYY-MM-DD: o período do resumo é obrigatório e não tem "
        + "default de servidor.";

    public const string MensagemAteObrigatorio =
        "Informe 'ate' no formato YYYY-MM-DD: o período do resumo é obrigatório e não tem "
        + "default de servidor.";

    /// <summary>
    /// 🔴 <b>Teto de DURAÇÃO — guarda de VOLUME, não regra de negócio.</b> 1830 dias = 5 anos
    /// (365 × 5 + 5 dias de folga para bissextos), contados <b>inclusive</b> nos dois
    /// extremos.
    ///
    /// <para>Ele existe por uma razão de implementação declarada: o resumo agrega em
    /// <b>memória</b> sobre uma única consulta de faixa que cobre o período <b>e o anterior</b>
    /// (ver <c>FinanceiroService</c>). Um período de milhares de anos tentaria materializar a
    /// tabela inteira. Nenhuma ruling do ciclo FIN diz que "5 anos é o máximo que um gestor
    /// pode pedir" — se a agregação um dia descer para o banco, esta constante deixa de ter
    /// motivo para existir e some junto.</para>
    ///
    /// <para>⚠️ <b>Ele NÃO substitui a guarda de calendário</b>, e essa confusão é o modo de
    /// falha mais provável de quem mexer aqui depois: <c>9999-12-01 → 9999-12-31</c> são 31
    /// dias, passam por este teto com folga, e mesmo assim estouram no <c>ate + 1 dia</c>.</para>
    /// </summary>
    public const int DuracaoMaximaEmDias = 1830;

    /// <summary>
    /// Último <c>ate</c> aceito: o último dia do calendário que ainda <b>tem dia seguinte</b>.
    /// O resumo converte <c>ate</c> no limite <b>exclusivo</b> <c>ate + 1 dia</c>, então
    /// <c>DateOnly.MaxValue</c> como <c>ate</c> não é um período grande demais — é um período
    /// <b>não representável</b>.
    /// </summary>
    public static readonly DateOnly UltimoAteAceito = DateOnly.MaxValue.AddDays(-1);

    public const string MensagemAteForaDoCalendario =
        "O fim do período ('ate') não pode ser 9999-12-31: o resumo soma o último dia INTEIRO, "
        + "e para isso usa o dia seguinte como limite exclusivo — que não existe no "
        + "calendário. Use no máximo 9999-12-30.";

    public const string MensagemDeForaDoCalendario =
        "O início do período ('de') está perto demais de 0001-01-01: o resumo compara com o "
        + "período de MESMA duração imediatamente anterior, e esse período cairia antes do "
        + "início do calendário. Escolha um 'de' posterior ou um período mais curto.";

    public const string MensagemDuracaoExcedida =
        "O período não pode passar de 1830 dias (5 anos), contados de 'de' até 'ate' "
        + "inclusive. O limite é de volume — o resumo agrega em memória — e não uma regra de "
        + "negócio: divida o intervalo em pedaços menores.";

    public const string MensagemIntervaloInvertido =
        "O início do período ('de') não pode ser posterior ao fim ('ate'). Um intervalo "
        + "invertido devolveria receita zero, indistinguível de um período sem faturamento.";

    public ResumoFinanceiroQueryValidator()
    {
        RuleFor(x => x.De).NotNull().WithMessage(MensagemDeObrigatorio);

        RuleFor(x => x.Ate).NotNull().WithMessage(MensagemAteObrigatorio);

        // A regra é sobre a COMBINAÇÃO, então o Must recebe o DTO inteiro — mesma forma da
        // regra de origem de valor em CobrancaCreateValidator. O .When evita que o intervalo
        // invertido seja reportado por cima de um campo que sequer veio: com 'de' ausente, a
        // mensagem útil é "informe de", não "intervalo invertido".
        RuleFor(x => x.Ate)
            .Must((dto, ate) => dto.De!.Value <= ate!.Value)
            .WithMessage(MensagemIntervaloInvertido)
            .When(x => x.De.HasValue && x.Ate.HasValue);

        // ── F1: borda SUPERIOR de calendário ────────────────────────────────
        // Independente de `de`: `ate + 1 dia` é calculado sempre, e é a primeira coisa que
        // estoura. Por isso esta regra NÃO tem `.When` cruzado com `de`.
        RuleFor(x => x.Ate)
            .Must(ate => ate!.Value <= UltimoAteAceito)
            .WithMessage(MensagemAteForaDoCalendario)
            .When(x => x.Ate.HasValue);

        // ── F1: borda INFERIOR de calendário ───────────────────────────────
        // Quem estoura aqui é o período ANTERIOR — um cálculo DERIVADO que o gestor nem
        // pediu. `De.DayNumber` é a distância em dias de `de` até 0001-01-01; se ela for
        // menor que a duração, `de − duração` cai antes do início do calendário. Comparação
        // por inteiro de propósito: `De.AddDays(-duracao)` é a chamada que lança.
        RuleFor(x => x.De)
            .Must((dto, de) => de!.Value.DayNumber >= DuracaoEmDias(dto))
            .WithMessage(MensagemDeForaDoCalendario)
            .When(EhIntervaloOrdenadoECompleto);

        // ── F1: teto de VOLUME ──────────────────────────────────────
        // Motivo diferente das duas acima (ver DuracaoMaximaEmDias). `<=` porque a duração é
        // contada INCLUSIVE nos dois extremos.
        RuleFor(x => x.Ate)
            .Must((dto, _) => DuracaoEmDias(dto) <= DuracaoMaximaEmDias)
            .WithMessage(MensagemDuracaoExcedida)
            .When(EhIntervaloOrdenadoECompleto);
    }

    /// <summary>
    /// Duração do período em dias, <b>inclusiva</b> nos dois extremos (<c>de == ate</c> é 1
    /// dia). Só é chamada sob <see cref="EhIntervaloOrdenadoECompleto"/>, então nunca produz
    /// número negativo.
    /// </summary>
    private static int DuracaoEmDias(ResumoFinanceiroQueryDto dto) =>
        dto.Ate!.Value.DayNumber - dto.De!.Value.DayNumber + 1;

    /// <summary>
    /// As duas datas vieram <b>e</b> estão em ordem. O <c>de &lt;= ate</c> faz parte da
    /// guarda porque um intervalo invertido já tem mensagem própria: reportar por cima dele
    /// "duração excedida" (com duração negativa) trocaria um erro acionável por um confuso.
    /// </summary>
    private static bool EhIntervaloOrdenadoECompleto(ResumoFinanceiroQueryDto dto) =>
        dto.De.HasValue && dto.Ate.HasValue && dto.De.Value <= dto.Ate.Value;
}
