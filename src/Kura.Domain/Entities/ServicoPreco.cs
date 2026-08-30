namespace Kura.Domain.Entities;

/// <summary>
/// Catálogo de preços da clínica (FD-08, ciclo FIN — ruling D-2): quanto a clínica
/// cobra por cada serviço.
///
/// <para><b>Tabela .NET-owned.</b> Criada pelo Flyway em <c>V18__financeiro.sql</c>
/// (repo <c>backend-tutor-java</c>, única autoridade de DDL do projeto). As
/// propriedades abaixo espelham aquele <c>CREATE TABLE</c> coluna a coluna; nomes,
/// dimensões e a precisão do valor vivem em <c>ServicoPrecoConfiguration</c>.</para>
///
/// <para><b>NÃO é fonte de valor de cobrança já lançada.</b> Alterar
/// <see cref="VlPreco"/> não altera nenhuma <see cref="Cobranca"/> existente —
/// <c>COBRANCA.VL_COBRADO</c> guarda uma cópia do valor no momento do lançamento.
/// Ler o valor por FK faria mudar preço de tabela reescrever o histórico financeiro
/// retroativamente. Ver o comentário da coluna na V18.</para>
///
/// <para><b>Sem UNIQUE por clínica em <see cref="NmServico"/>, de propósito:</b> com
/// soft delete, uma unique impediria recadastrar um serviço depois de desativado.</para>
///
/// <para><b>Escopo desta task (FD-08):</b> domínio, mapeamento e isolamento de
/// tenant. CRUD/endpoint é a FD-09; KPI é a FD-11.</para>
/// </summary>
public class ServicoPreco : EntidadeBase
{
    /// <summary>
    /// <c>ID_CLINICA</c> — NOT NULL na V18. Tabela de preço é sempre de uma clínica:
    /// é a chave do isolamento multi-tenant (ver <c>KuraDbContext.ApplyTenantFilters</c>).
    /// </summary>
    public long IdClinica { get; set; }

    /// <summary>
    /// <c>NM_SERVICO VARCHAR2(200) NOT NULL</c> — nome do serviço como o gestor o cadastra.
    /// </summary>
    public string NmServico { get; set; } = string.Empty;

    /// <summary>
    /// <c>VL_PRECO NUMBER(10,2) NOT NULL</c> — preço de tabela vigente.
    ///
    /// <para>🔴 <b><c>decimal</c>, nunca <c>double</c>/<c>float</c>.</b> Dinheiro em
    /// binário de ponto flutuante não representa exatamente valores decimais comuns
    /// (0,1 e 0,07 não têm representação finita em base 2), então soma de centavos
    /// acumula erro e o round-trip com o Oracle deixa de ser idêntico. A precisão
    /// <c>(10,2)</c> é declarada explicitamente em <c>ServicoPrecoConfiguration</c>
    /// e travada por teste em <c>ServicoPrecoTenantIsolationTests</c> — o provider
    /// InMemory da suíte NÃO reprova precisão errada, nem tipo errado.</para>
    ///
    /// <para>O modo de falha, medido na FD-07 do lado Java: trocar <c>NUMBER(10,2)</c>
    /// por <c>NUMBER(10)</c> faz <c>999.99</c> virar <c>1000</c> <b>em silêncio</b> —
    /// arredondamento, não exceção.</para>
    /// </summary>
    public decimal VlPreco { get; set; }
}
