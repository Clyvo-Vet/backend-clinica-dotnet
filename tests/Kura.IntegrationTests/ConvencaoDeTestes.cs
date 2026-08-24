namespace Kura.IntegrationTests;

/// <summary>
/// S3D-07 — nomes da convenção <b>Unit × Integration</b>, centralizados para que o
/// <c>--filter</c> documentado em <c>tests/README.md</c> não dependa de string digitada à
/// mão em cada classe.
///
/// <para>
/// <b>Por que a convenção precisa de um trait e não só de um parágrafo.</b> Este repositório
/// separa testes por <b>camada de arquitetura</b> (<c>Kura.Domain.Tests</c>,
/// <c>Kura.Application.Tests</c>, <c>Kura.Infrastructure.Tests</c>), enquanto a rubrica da
/// Sprint 3 nomeia o par <b>(Unit, Integration)</b>. Os dois recortes convivem: os 3
/// projetos por camada são <b>unitários</b> (rodam em processo, sem host HTTP e sem banco
/// real), e <c>Kura.IntegrationTests</c> é o projeto de <b>integração</b> (sobe o
/// <c>Program.cs</c> real e faz requisições HTTP de ponta a ponta).
/// </para>
///
/// <para>
/// Com este trait a separação deixa de ser só documental e vira executável em <b>qualquer</b>
/// recorte, inclusive rodando a solução inteira:
/// <code>
/// dotnet test KuraApi.slnx --filter "Categoria=Integracao"    # só integração
/// dotnet test KuraApi.slnx --filter "Categoria!=Integracao"   # só unitários
/// </code>
/// ⚠️ O segundo filtro funciona porque o <c>!=</c> do VSTest também casa teste que <b>não
/// declara</b> a propriedade — comportamento MEDIDO nesta task (ver
/// <c>task-S3D-07-report.md</c> §2), não presumido da documentação. Foi por causa dele que
/// os ~40 arquivos de teste unitário NÃO precisaram ser anotados um a um.
/// </para>
/// </summary>
internal static class ConvencaoDeTestes
{
    /// <summary>Nome da propriedade de trait usada no <c>--filter</c>.</summary>
    public const string Categoria = "Categoria";

    /// <summary>Valor aplicado às classes que sobem o host real e falam HTTP.</summary>
    public const string Integracao = "Integracao";
}
