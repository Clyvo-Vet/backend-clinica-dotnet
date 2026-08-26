namespace Kura.IntegrationTests;

/// <summary>
/// S3D-07 — <b>Collection Fixture</b>: uma única <see cref="KuraApiFactory"/> compartilhada
/// pelas classes de integração que precisam do <b>mesmo host semeado</b>
/// (<see cref="AutenticacaoHttpTests"/> e <see cref="FluxoDeNegocioHttpTests"/>).
///
/// <para>
/// <b>Por que existe.</b> Cada instância de <see cref="KuraApiFactory"/> sobe um host
/// ASP.NET Core completo a partir do <c>Program.cs</c> real — Serilog, JWT,
/// FluentValidation, health checks, OpenTelemetry, pipeline de middlewares — e ainda semeia
/// duas clínicas com hash BCrypt (lento de propósito). Com <c>IClassFixture</c>, como a
/// S3D-06 entregou, esse custo era pago uma vez <b>por classe</b>. Medição direta,
/// instrumentando <c>Semear</c> para gravar um arquivo por execução: <b>3 execuções</b> na
/// baseline contra <b>2</b> aqui — e o trabalho total da suíte (soma das durações por teste
/// no <c>.trx</c>) caiu de <b>9,2s</b> para <b>5,3s</b> quando as 3 classes dividiam uma
/// collection só.
/// </para>
///
/// <para>
/// 🔴 <b>Compartilhar fixture NÃO é automaticamente mais rápido — e o critério de aceite
/// desta task exigiu medir em vez de presumir.</b> O xUnit paraleliza <b>collections</b>,
/// não classes dentro de uma collection. Colocar as <b>3</b> classes numa collection só
/// eliminou 2 dos 3 bootstraps e ainda assim ficou <b>MAIS LENTA</b>: <b>6,71s</b> contra
/// <b>5,61s</b> da baseline (+19,6%, 4 execuções cada, wall de processo). O trabalho total
/// caiu 42% e o relógio subiu 20% — porque o paralelismo entre 3 collections valia mais que
/// os bootstraps duplicados.
/// </para>
///
/// <para>
/// <b>Por isso <see cref="AmbienteEFiacaoDoHostTests"/> ficou de fora</b>, com
/// <c>IClassFixture</c> próprio: ela carrega o teste do <c>/health</c>, ~2,1s de timeout de
/// discagem que <b>não é bootstrap</b> e não some com fixture compartilhada (medido: custa o
/// mesmo nos dois arranjos). Numa collection separada esses ~2,1s rodam <b>em paralelo</b>
/// com esta. Resultado: <b>5,51s</b>, ou seja, o compartilhamento passou a caber dentro do
/// orçamento de tempo da baseline. Os três arranjos estão medidos lado a lado em
/// <c>task-S3D-07-report.md</c> §1.
/// </para>
///
/// <para>
/// <b>Consequência de correção, não de desempenho:</b> as 2 classes desta collection
/// enxergam <b>o mesmo banco InMemory</b> (o nome do banco é privado de cada instância de
/// fábrica). Escrita feita por uma fica visível para a outra. Isso é seguro nesta suíte
/// porque o único endpoint de escrita exercitado é <c>POST /api/v1/veterinarios</c> (em
/// <see cref="FluxoDeNegocioHttpTests"/>) e nenhuma asserção depende da <b>cardinalidade</b>
/// da lista de veterinários nem do valor da PK gerada — as asserções são
/// <c>Contain</c>/<c>NotContain</c>/<c>OnlyContain</c>. Provado por medição, não por leitura:
/// os 19 testes passam <b>individualmente</b>, cada um em processo próprio, e a suíte fica
/// verde com a ordem de execução <b>invertida</b> por um <c>ITestCaseOrderer</c> descartável
/// — arranjo em que <c>Listar_veterinarios</c> roda ANTES de <c>Criar_veterinario</c>, o
/// inverso da ordem padrão (<c>task-S3D-07-report.md</c> §3).
/// <b>Quem acrescentar teste aqui herda essa restrição:</b> asserção do tipo
/// <c>HaveCount(n)</c> sobre um recurso que qualquer teste desta collection crie passa a
/// depender da ordem de execução — que o xUnit não garante.
/// </para>
///
/// <para>
/// ⚠️ <b>Não existe caminho que dê fixture compartilhada E paralelismo total neste stack:</b>
/// <c>AssemblyFixture</c> (uma instância para todas as collections) só existe no xUnit v3, e
/// este projeto está no <b>xUnit 2.9.3</b> — verificado no binário do pacote, onde
/// <c>IClassFixture</c>/<c>ICollectionFixture</c>/<c>CollectionDefinition</c> aparecem e
/// <c>IAssemblyFixture</c> não aparece. A divisão acima é o melhor arranjo disponível na v2.
/// </para>
///
/// <para>
/// O nome literal <c>"Integration"</c> é o que a rubrica nomeia. A classe é só o portador do
/// atributo: o xUnit nunca a instancia, ela existe para casar
/// <c>[CollectionDefinition]</c> com <c>ICollectionFixture&lt;T&gt;</c>.
/// </para>
/// </summary>
[CollectionDefinition(Nome)]
public class ColecaoDeIntegracao : ICollectionFixture<KuraApiFactory>
{
    /// <summary>
    /// Nome da collection. Constante para que <c>[Collection(...)]</c> nas classes de teste
    /// não seja string solta repetida em vários arquivos. Um literal com erro de digitação
    /// não degrada em silêncio — a classe cairia numa collection sem fixture definida e o
    /// xUnit falha na construção ("constructor parameters did not have matching fixture
    /// data") — mas a constante torna a ligação verificável pelo compilador em vez de pelo
    /// runner.
    /// </summary>
    public const string Nome = "Integration";
}
