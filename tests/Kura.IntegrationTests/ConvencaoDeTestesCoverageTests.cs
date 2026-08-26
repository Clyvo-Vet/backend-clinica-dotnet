namespace Kura.IntegrationTests;

using System.Reflection;
using FluentAssertions;

/// <summary>
/// G2 da S3D-07 — trava por reflection a convenção que o <c>--filter</c> depende.
///
/// <para>
/// <b>Por que existe.</b> A verificação usada até aqui era «<c>19 + 285 = 304</c>», e ela é
/// estruturalmente incapaz de detectar o defeito: medido por mutação no G2, removendo o
/// <c>[Trait]</c> de <see cref="AutenticacaoHttpTests"/> os 7 testes migram para o balde
/// unitário (<b>12 + 292 = 304</b>) com <b>0 falhas</b> — a soma fecha igual nos dois mundos,
/// porque o <c>!=</c> do VSTest absorve quem não declara a propriedade.
/// </para>
///
/// <para>
/// Mesmo padrão de <c>TenantFilterCoverageTests</c> em <c>Kura.Infrastructure.Tests</c>:
/// derivar a lista <b>do código</b> em vez de mantê-la à mão. Regra de ouro v7 do projeto —
/// inventário escrito à mão apodrece em silêncio.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class ConvencaoDeTestesCoverageTests
{
    [Fact]
    public void Toda_classe_deste_projeto_com_testes_declara_o_trait_de_integracao()
    {
        // Arrange — a lista sai do assembly, não de um inventário mantido à mão.
        var classesComTeste = typeof(KuraApiFactory).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any()))
            .OrderBy(t => t.Name)
            .ToList();

        classesComTeste.Should().NotBeEmpty(
            "se a varredura não achar nenhuma classe de teste, o instrumento está quebrado "
            + "e o resultado verde não significa nada");

        // Act
        var semTrait = classesComTeste
            .Where(t => !t.GetCustomAttributesData().Any(a =>
                a.AttributeType == typeof(TraitAttribute)
                && a.ConstructorArguments.Count == 2
                && (string?)a.ConstructorArguments[0].Value == ConvencaoDeTestes.Categoria
                && (string?)a.ConstructorArguments[1].Value == ConvencaoDeTestes.Integracao))
            .Select(t => t.Name)
            .ToList();

        // Assert
        semTrait.Should().BeEmpty(
            "toda classe de Kura.IntegrationTests precisa de "
            + "[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)], senão seus "
            + "testes caem em silêncio no recorte 'unitário' do --filter e a separação "
            + "Unit x Integration apodrece sem ninguém perceber");
    }
}
