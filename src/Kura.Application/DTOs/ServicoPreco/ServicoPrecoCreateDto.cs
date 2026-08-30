namespace Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// Corpo de criação de um item da tabela de preços (FD-09, ciclo FIN).
///
/// <para>
/// 🔴 <b>NÃO existe campo <c>IdClinica</c>, e isso é a regra da task, não esquecimento.</b>
/// A clínica sai do <c>clinicaId</c> do JWT dentro de <c>ServicoPrecoService</c> e de mais
/// lugar nenhum. É o padrão da <c>FD-04</c> e a correção da <c>FD-05</c>, onde
/// <c>VeterinarioService.CreateAsync</c> grava <c>dto.IdClinica</c> sem comparar com o token
/// — e por isso qualquer clínica autenticada cria veterinário dentro de outra. Aceitar o
/// campo e comparar com o token deixaria a garantia dependendo de alguém lembrar da
/// comparação em cada caminho novo, para sempre; aqui o campo <b>não existe</b>.
/// </para>
/// </summary>
public sealed class ServicoPrecoCreateDto
{
    /// <summary><c>NM_SERVICO VARCHAR2(200) NOT NULL</c>.</summary>
    public string NmServico { get; init; } = string.Empty;

    /// <summary>
    /// <c>VL_PRECO NUMBER(10,2) NOT NULL</c>, com <c>CHECK (VL_PRECO &gt;= 0)</c> no Oracle.
    ///
    /// <para>🔴 <c>decimal</c>, nunca <c>double</c> — ver <c>ServicoPreco.VlPreco</c>. O piso
    /// de zero é replicado em <c>ServicoPrecoCreateValidator</c>: sem ele, um preço negativo
    /// só falharia no banco, como <c>ORA-02290</c> traduzido em <c>500</c>.</para>
    /// </summary>
    public decimal VlPreco { get; init; }
}
