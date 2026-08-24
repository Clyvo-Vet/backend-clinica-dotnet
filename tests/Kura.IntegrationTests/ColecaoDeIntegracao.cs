namespace Kura.IntegrationTests;

/// <summary>
/// S3D-07 — <b>Collection Fixture</b>: uma única <see cref="KuraApiFactory"/> compartilhada
/// por TODAS as classes de teste de integração, em vez de uma instância por classe.
///
/// <para>
/// <b>Por que existe (o ganho é medido, não presumido).</b> Cada instância de
/// <see cref="KuraApiFactory"/> sobe um host ASP.NET Core completo a partir do
/// <c>Program.cs</c> real — Serilog, JWT, FluentValidation, health checks, OpenTelemetry,
/// pipeline de middlewares — e ainda semeia duas clínicas com hash BCrypt (que é lento de
/// propósito). Com <c>IClassFixture</c>, como a S3D-06 entregou, esse custo era pago
/// <b>3 vezes</b>, uma por classe. Medição por teste na baseline (arquivo <c>.trx</c>,
/// commit <c>5ba131c</c>): os 3 primeiros testes de cada classe custavam
/// <c>2,66s</c> / <c>2,22s</c> / <c>2,15s</c>, enquanto os 15 testes restantes somavam
/// <c>1,4s</c> no total. Ou seja: o tempo da suíte era quase inteiramente bootstrap de
/// host, não trabalho de teste.
/// </para>
///
/// <para>
/// ⚠️ <b>O ganho NÃO é automático, e por isso o critério de aceite desta task exige medir.</b>
/// O xUnit paraleliza <b>collections</b>, não classes dentro de uma collection. Antes desta
/// mudança as 3 classes eram 3 collections implícitas e rodavam <b>em paralelo</b> (3 hosts
/// simultâneos); agora são uma collection só e rodam <b>em sequência</b> (1 host). Trocamos
/// 3 bootstraps concorrentes por 1 bootstrap e execução serializada — o efeito líquido é
/// empírico. Números antes/depois estão em <c>task-S3D-07-report.md</c>.
/// </para>
///
/// <para>
/// <b>Consequência de correção, não de desempenho:</b> as 3 classes passam a enxergar
/// <b>o mesmo banco InMemory</b> (o nome do banco é privado de cada instância de fábrica).
/// Escrita feita por uma classe fica visível para as outras. Isso é seguro nesta suíte
/// porque o único endpoint de escrita exercitado é
/// <c>POST /api/v1/veterinarios</c> (em <see cref="FluxoDeNegocioHttpTests"/>) e nenhuma
/// asserção da suíte depende da <b>cardinalidade</b> da lista de veterinários nem do valor
/// da PK gerada — ver a nota em
/// <c>Listar_veterinarios_autenticado_devolve_200_com_o_veterinario_da_clinica</c>.
/// <b>Quem acrescentar teste aqui herda essa restrição:</b> asserção do tipo
/// <c>HaveCount(n)</c> sobre um recurso que qualquer teste da suíte crie passa a depender
/// da ordem de execução — que o xUnit não garante.
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
    /// não seja string solta repetida em 3 arquivos. Um literal com erro de digitação não
    /// degrada em silêncio — a classe cairia numa collection sem fixture definida e o xUnit
    /// falha na construção ("constructor parameters did not have matching fixture data") —
    /// mas a constante torna a ligação verificável pelo compilador em vez de pelo runner.
    /// </summary>
    public const string Nome = "Integration";
}
