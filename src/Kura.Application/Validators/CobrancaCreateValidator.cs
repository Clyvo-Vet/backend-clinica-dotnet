namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Cobranca;

/// <summary>
/// FD-10 — contrato de entrada do lançamento de cobrança.
///
/// <para>
/// 🔴 <b>O piso de zero existe porque o Oracle tem <c>CHK_COBRANCA_VALOR CHECK (VL_COBRADO
/// &gt;= 0)</c> e o <c>.NET</c> não tinha nada equivalente.</b> Medido na FD-09 sobre a
/// coluna irmã: sem a regra, <c>{"vlCobrado": -1}</c> atravessa validator, service e EF sem
/// uma única objeção, o InMemory da suíte <b>grava</b> e devolve <c>201</c> (ele não aplica
/// CHECK constraint nenhuma), e só produção morre — como <c>ORA-02290</c> traduzido em
/// <c>500</c> pelo <c>ExceptionHandlerMiddleware</c>. O detector do banco não alcança o
/// teste; este validator alcança.
/// </para>
///
/// <para>
/// <b><c>PrecisionScale(10, 2)</c> espelha <c>NUMBER(10,2)</c>, e pelo mesmo tipo de
/// motivo.</b> O modo de falha do outro lado da faixa foi medido na FD-07: valor que não cabe
/// na escala declarada é <b>arredondado em silêncio</b> pelo Oracle, não recusado. Aqui o
/// dano é maior que em <c>VL_PRECO</c>, porque esta é a coluna que a FD-11 SOMA — erro de
/// centavo por linha vira erro de relatório agregado.
/// </para>
///
/// <para>
/// 🔴 <b>A faixa de <see cref="CobrancaCreateDto.DtCobranca"/> não é preciosismo.</b>
/// <c>DateTime</c> é struct: um corpo com <c>"dtCobranca": "0001-01-01T00:00:00"</c> produz
/// um valor que <b>não é nulo</b>, passa por qualquer guarda de null, satisfaz o <c>NOT
/// NULL</c> do Oracle e <b>desaparece de todo KPI por período da FD-11</b> — receita lançada,
/// gravada e invisível (achado F2 da revisão G2 da FD-08). O piso de <c>2000-01-01</c> é o
/// que transforma esse corpo em <c>400</c> em vez de linha fantasma. O teto (hoje + 1 dia)
/// fecha o lado oposto: lançamento datado no futuro infla o mês corrente com receita que
/// ainda não aconteceu. O dia de folga é tolerância de fuso/relógio — o cliente manda hora
/// local, o servidor compara em UTC.
/// </para>
///
/// <para>
/// ⚠️ <b>Todas as regras opcionais estão escritas sobre a propriedade NULLABLE</b>
/// (<c>RuleFor(x =&gt; x.VlCobrado)</c>, e não <c>x.VlCobrado!.Value</c>). Não é estilo: o
/// valor da propriedade é lido pelo FluentValidation <b>antes</b> de a condição do
/// <c>.When()</c> ser consultada, então a segunda forma desreferencia o nulo que a condição
/// existia para evitar. Os <c>.When()</c> abaixo são redundantes com a semântica dos
/// comparadores (que passam em cima de nulo) e estão escritos assim mesmo — a intenção "esta
/// regra só vale quando o campo veio" fica no código, não na trivia da biblioteca.
/// </para>
/// </summary>
public sealed class CobrancaCreateValidator : AbstractValidator<CobrancaCreateDto>
{
    /// <summary>
    /// Maior valor representável em <c>NUMBER(10,2)</c>: 8 dígitos inteiros + 2 decimais.
    /// Constante para que a mensagem de erro e a regra não possam divergir.
    /// </summary>
    public const decimal ValorMaximo = 99_999_999.99m;

    /// <summary>
    /// Piso da data de cobrança. Escolhido para capturar <c>0001-01-01</c>
    /// (<c>default(DateTime)</c>) sem precisar de uma regra que compare com o default —
    /// nenhuma clínica lança receita retroativa ao ano 2000.
    /// </summary>
    public static readonly DateTime DataMinima = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Folga sobre <c>UtcNow</c> para absorver fuso e relógio do cliente.</summary>
    public static readonly TimeSpan ToleranciaFutura = TimeSpan.FromDays(1);

    public const string MensagemValorNegativo =
        "Valor cobrado não pode ser negativo (o banco recusa com CHECK VL_COBRADO >= 0).";

    public const string MensagemSemOrigemDeValor =
        "Informe vlCobrado, idServicoPreco, ou os dois: sem nenhum dos dois não há como "
        + "determinar o valor da cobrança.";

    public const string MensagemDataForaDaFaixa =
        "Data de cobrança fora da faixa aceita: não pode ser anterior a 2000-01-01 (o que "
        + "captura 0001-01-01, valor que passaria pelo NOT NULL do banco e sumiria de todo "
        + "relatório por período) nem posterior a amanhã.";

    public CobrancaCreateValidator()
    {
        // 🔴 A regra de origem do valor é sobre a COMBINAÇÃO dos dois campos, e por isso o
        // Must recebe o DTO inteiro. Escrita só sobre VlCobrado com um .When cruzado, o
        // caso "nenhum dos dois veio" ficaria sem regra nenhuma e o service teria de
        // adivinhar o valor.
        RuleFor(x => x.VlCobrado)
            .Must((dto, valor) => valor.HasValue || dto.IdServicoPreco.HasValue)
            .WithMessage(MensagemSemOrigemDeValor);

        RuleFor(x => x.VlCobrado)
            .GreaterThanOrEqualTo(0).WithMessage(MensagemValorNegativo)
            .LessThanOrEqualTo(ValorMaximo)
            .WithMessage($"Valor cobrado deve ser no máximo {ValorMaximo} (coluna NUMBER(10,2)).")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("Valor cobrado deve ter no máximo 2 casas decimais (coluna NUMBER(10,2)).")
            .When(x => x.VlCobrado.HasValue);

        RuleFor(x => x.IdServicoPreco)
            .GreaterThan(0)
            .WithMessage("idServicoPreco deve ser um identificador positivo.")
            .When(x => x.IdServicoPreco.HasValue);

        RuleFor(x => x.DsFormaPagamento)
            .MaximumLength(30)
            .WithMessage("Forma de pagamento deve ter no máximo 30 caracteres (VARCHAR2(30)).");

        RuleFor(x => x.DtCobranca)
            .Must(data => !data.HasValue || SerDataAceita(data.Value))
            .WithMessage(MensagemDataForaDaFaixa);
    }

    /// <summary>
    /// Faixa aceita para a data informada. O teto é recalculado a cada chamada de propósito:
    /// capturá-lo numa constante estática travaria a fronteira no instante em que a classe
    /// foi carregada, e um processo de vida longa passaria a recusar "hoje".
    ///
    /// <para>O piso compara o valor cru (sem converter fuso) porque o alvo dele é
    /// <c>0001-01-01</c>, e converter <c>DateTime.MinValue</c> entre fusos é justamente o
    /// ponto onde o .NET satura em silêncio. O teto compara em UTC, que é o fuso em que o
    /// servidor grava.</para>
    /// </summary>
    public static bool SerDataAceita(DateTime data) =>
        data >= DataMinima && data.ToUniversalTime() <= DateTime.UtcNow.Add(ToleranciaFutura);
}
