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
/// </summary>
public sealed class ResumoFinanceiroQueryValidator : AbstractValidator<ResumoFinanceiroQueryDto>
{
    public const string MensagemDeObrigatorio =
        "Informe 'de' no formato YYYY-MM-DD: o período do resumo é obrigatório e não tem "
        + "default de servidor.";

    public const string MensagemAteObrigatorio =
        "Informe 'ate' no formato YYYY-MM-DD: o período do resumo é obrigatório e não tem "
        + "default de servidor.";

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
    }
}
