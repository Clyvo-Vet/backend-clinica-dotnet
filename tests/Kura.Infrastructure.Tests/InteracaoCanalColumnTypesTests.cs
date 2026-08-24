namespace Kura.Infrastructure.Tests;

using FluentAssertions;

/// <summary>
/// TASK-86 (item 5, higiene): trava que as 3 colunas numéricas de InteracaoCanal
/// (ID_INTERACAO/ID_CLINICA/ID_TUTOR) declaram HasColumnType("NUMBER(10)") no EF,
/// alinhado ao que V15__interacao_canal.sql (Flyway, backend-tutor-java) de fato cria —
/// confirmado na fonte nesta task. Antes desta task, o provider Oracle.EntityFrameworkCore
/// mapeava `long`/`long?` para NUMBER(19) por padrão, divergindo em silêncio da coluna real.
///
/// Este teste lê a metadata do IModel do EF (via HasColumnType/GetColumnType), não executa
/// SQL contra Oracle nem valida o schema real — a metadata do EF existe e é lida
/// independente do provider (InMemory ignora a anotação ao gerar consultas, mas não a
/// remove do modelo). Por isso este teste NÃO prova que o schema real bate — Flyway
/// continua sendo a única autoridade de DDL neste projeto — só prova que o modelo do EF
/// não regride para NUMBER(19)/sem HasColumnType de novo.
///
/// <para>
/// S3D-07: o <c>CreateContext()</c> estático, que montava um banco InMemory novo por caso
/// de teste só para ler metadata, virou <see cref="ModeloEfInMemoryFixture"/> injetada por
/// <c>IClassFixture</c> — este é o exemplo do padrão <b>Class Fixture</b> no lado
/// <b>unitário</b> da suíte (o lado de integração usa <b>Collection Fixture</b>, em
/// <c>Kura.IntegrationTests.ColecaoDeIntegracao</c>). O compartilhamento é seguro aqui, e
/// só aqui, porque esta classe lê <b>metadata imutável</b> e nunca grava; ver a
/// documentação da fixture para o porquê de as classes de repositório continuarem com
/// banco próprio por teste.
/// </para>
/// </summary>
public class InteracaoCanalColumnTypesTests : IClassFixture<ModeloEfInMemoryFixture>
{
    private readonly ModeloEfInMemoryFixture _modelo;

    public InteracaoCanalColumnTypesTests(ModeloEfInMemoryFixture modelo) => _modelo = modelo;

    [Theory]
    [InlineData("Id", "NUMBER(10)")]
    [InlineData("IdClinica", "NUMBER(10)")]
    [InlineData("IdTutor", "NUMBER(10)")]
    public void PropriedadesNumericas_DeInteracaoCanal_DeclaramColumnTypeAlinhadoAoFlyway(
        string nomePropriedade, string columnTypeEsperado)
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Kura.Domain.Entities.InteracaoCanal));

        entityType.Should().NotBeNull();

        var propriedade = entityType!.FindProperty(nomePropriedade);
        propriedade.Should().NotBeNull(
            $"InteracaoCanal deve declarar a propriedade {nomePropriedade}");

        // GetColumnType() resolve o type mapping efetivo do provider (falha com o
        // provider InMemory, que não tem RelationalTypeMapping). Lemos a anotação
        // "Relational:ColumnType" diretamente — é exatamente o valor configurado por
        // HasColumnType(...) no Configuration, independente de provider.
        var columnTypeConfigurado = propriedade!
            .FindAnnotation("Relational:ColumnType")?.Value as string;

        columnTypeConfigurado.Should().Be(columnTypeEsperado,
            $"{nomePropriedade} deve bater com a precisão real de V15__interacao_canal.sql " +
            "(backend-tutor-java) — NUMBER(10), não o NUMBER(19) que o provider Oracle " +
            "infere por padrão para long/long?");
    }
}
