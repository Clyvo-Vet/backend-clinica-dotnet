namespace Kura.Domain.Entities;

/// <summary>
/// Lançamento financeiro (FD-08, ciclo FIN — ruling D-3), pendurado no atendimento
/// que aconteceu.
///
/// <para><b>Tabela .NET-owned.</b> Criada pelo Flyway em <c>V18__financeiro.sql</c>
/// (repo <c>backend-tutor-java</c>). As propriedades espelham aquele
/// <c>CREATE TABLE</c> coluna a coluna; nomes, dimensões e a precisão do valor
/// vivem em <c>CobrancaConfiguration</c>.</para>
///
/// <para><b>Escopo negativo declarado na V18:</b> sem gateway, transação externa,
/// status de processamento ou conciliação (D-1); sem imposto, repasse ou margem
/// (D-6); sem parcelamento, múltiplas formas na mesma cobrança ou estorno (FD-10).</para>
///
/// <para><b>Escopo desta task (FD-08):</b> domínio, mapeamento e isolamento de
/// tenant. O endpoint de lançamento é a FD-10; os KPI são a FD-11.</para>
/// </summary>
public class Cobranca : EntidadeBase
{
    /// <summary>
    /// <c>ID_EVENTO_CLINICO NUMBER(10) NOT NULL</c> — atendimento que originou a
    /// cobrança. Não existe lançamento sem atendimento (D-3).
    ///
    /// <para>⚠️ A FK aponta <c>EVENTO_CLINICO(ID_EVENTO)</c>: a PK tem nome
    /// diferente da coluna que a referencia.</para>
    /// </summary>
    public long IdEventoClinico { get; set; }

    /// <summary>
    /// <c>ID_CLINICA NUMBER(10) NOT NULL</c> — clínica do lançamento. Denormalizado
    /// de <c>EVENTO_CLINICO</c> de propósito: é a coluna que o
    /// <c>ApplyTenantFilters</c> exige e a que os KPI da FD-11 agrupam. Manter
    /// coerente com o evento é responsabilidade do service (FD-10).
    /// </summary>
    public long IdClinica { get; set; }

    /// <summary>
    /// <c>ID_SERVICO_PRECO NUMBER(10)</c> — <b>NULLABLE</b> na V18: valor avulso, sem
    /// serviço tabelado, é lançamento legítimo (D-2). É rastreabilidade de <b>origem</b>
    /// (o mix por serviço da FD-11), <b>nunca</b> fonte de valor.
    /// </summary>
    public long? IdServicoPreco { get; set; }

    /// <summary>
    /// <c>VL_COBRADO NUMBER(10,2) NOT NULL</c> — valor efetivamente cobrado,
    /// <b>copiado</b> no momento do lançamento.
    ///
    /// <para>🔴 <b><c>decimal</c>, nunca <c>double</c>/<c>float</c></b> — mesmo
    /// argumento de <see cref="ServicoPreco.VlPreco"/>: em ponto flutuante binário o
    /// centavo não é exato, a soma acumula erro e o round-trip com o Oracle deixa de
    /// ser idêntico. A precisão <c>(10,2)</c> é declarada explicitamente em
    /// <c>CobrancaConfiguration</c> e travada por teste — InMemory não reprova nem
    /// tipo errado nem precisão errada.</para>
    ///
    /// <para><b>Coluna própria de propósito, não redundância:</b> ler o valor por FK
    /// em <c>SERVICO_PRECO</c> faria mudar preço de tabela reescrever o histórico
    /// financeiro retroativamente. NÃO remover por parecer duplicado.</para>
    /// </summary>
    public decimal VlCobrado { get; set; }

    /// <summary>
    /// <c>DS_FORMA_PAGAMENTO VARCHAR2(30)</c> — nullable e sem <c>CHECK</c> na V18:
    /// exigi-lo forçaria o veterinário a preenchê-lo no meio do atendimento, e lista
    /// fechada em schema é inventário manual que apodrece. <b>Não é status de
    /// processamento</b> (D-1).
    /// </summary>
    public string? DsFormaPagamento { get; set; }

    /// <summary>
    /// <c>DT_COBRANCA TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL</c> — data do
    /// lançamento. Linha com data nula seria invisível a todo KPI por período (FD-11):
    /// receita lançada que nenhum relatório enxerga.
    ///
    /// <para><b>O inicializador é a contrapartida do <c>DEFAULT CURRENT_TIMESTAMP</c> da
    /// V18, e existe por um modo de falha específico</b> (achado F2 da revisão G2):
    /// <c>DateTime</c> é struct, então "esquecer de setar" não produz nulo — produz
    /// <c>0001-01-01</c>. Esse valor <b>não é nulo</b>, então nenhuma guarda de null o
    /// pega; ele passa pelo <c>NOT NULL</c> do Oracle sem reclamar e some de todo KPI
    /// por período da FD-11, que filtra por intervalo de datas. Receita lançada,
    /// gravada, e invisível.</para>
    ///
    /// <para><b>Por que inicializador CLR e não <c>HasDefaultValueSql</c>:</b> é o padrão
    /// já estabelecido neste repo para exatamente esta forma — <c>DT_CRIACAO</c> também
    /// tem <c>DEFAULT CURRENT_TIMESTAMP</c> na V18 e <c>EntidadeBase.DtCriacao</c> a
    /// resolve com <c>= DateTime.UtcNow</c>. E o <c>HasDefaultValueSql</c> seria
    /// <b>inerte</b> aqui: o EF só delega ao default do banco quando a propriedade está
    /// no valor CLR default — ou seja, ele só salvaria o caso que este inicializador
    /// já impede de existir, e <b>só contra Oracle</b> (o InMemory da suíte não aplica
    /// default nenhum, então o <c>0001-01-01</c> continuaria vivo em teste).</para>
    ///
    /// <para>⚠️ <b>Isto reduz o dano, não dispensa a FD-10:</b> o inicializador dá a
    /// data em que o objeto foi <i>construído</i>. Quem lança uma cobrança com data
    /// retroativa (fechamento do dia anterior) tem que setá-la explicitamente — o
    /// default é uma rede, não a regra.</para>
    /// </summary>
    public DateTime DtCobranca { get; set; } = DateTime.UtcNow;
}
