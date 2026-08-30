namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Mapeamento de <see cref="Cobranca"/> (FD-08, ciclo FIN).
///
/// <para>Fonte da verdade do schema: <c>V18__financeiro.sql</c> no repo
/// <c>backend-tutor-java</c>. Cada dimensão abaixo foi copiada daquele
/// <c>CREATE TABLE</c>, não inferida.</para>
///
/// <para>⚠️ <b>Nenhum <c>HasQueryFilter</c> aqui, de propósito</b> — mesmo argumento
/// de <see cref="ServicoPrecoConfiguration"/> (dois filtros anônimos produzem
/// substituição silenciosa; a configuração roda antes do <c>ApplyTenantFilters</c>).
/// A guarda é
/// <c>CobrancaTenantIsolationTests.Configuracao_NaoDeclaraQueryFilterProprio</c>.</para>
///
/// <para><b>Sem navegações (<c>HasOne</c>/<c>Include</c>) nesta task, de propósito.</b>
/// As FKs existem no Oracle (<c>FK_COBRANCA_EVENTO</c>, <c>FK_COBRANCA_CLINICA</c>,
/// <c>FK_COBRANCA_SERVICO</c>) e o EF não as executa. Declarar navegação obrigatória
/// para <c>EventoClinico</c> traria o efeito que mordeu a TASK-63/FIX_4: o query
/// filter da entidade referenciada cascateia para o <c>Include</c> e derruba a linha
/// PAI inteira (uma cobrança some se o evento estiver soft-deletado). Se a FD-10/FD-11
/// precisar de join, que seja uma decisão explícita daquela task, medida.</para>
/// </summary>
public class CobrancaConfiguration : IEntityTypeConfiguration<Cobranca>
{
    public void Configure(EntityTypeBuilder<Cobranca> builder)
    {
        builder.ToTable("COBRANCA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_COBRANCA")
            .HasColumnType("NUMBER(10)")
            .HasDefaultValueSql("SEQ_COBRANCA.NEXTVAL");

        // ⚠️ A coluna se chama ID_EVENTO_CLINICO, mas a PK que ela referencia é
        // EVENTO_CLINICO.ID_EVENTO (nomes diferentes — registrado no comentário da
        // própria V18 para que ninguém "corrija" um dos dois lados).
        builder.Property(e => e.IdEventoClinico)
            .HasColumnName("ID_EVENTO_CLINICO")
            .HasColumnType("NUMBER(10)")
            .IsRequired();

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .HasColumnType("NUMBER(10)")
            .IsRequired();

        // NULLABLE na V18 (valor avulso sem serviço tabelado é lançamento legítimo,
        // D-2). Sem IsRequired() — o tipo CLR já é `long?`, mas a ausência aqui é
        // deliberada e documentada, não esquecimento.
        builder.Property(e => e.IdServicoPreco)
            .HasColumnName("ID_SERVICO_PRECO")
            .HasColumnType("NUMBER(10)");

        // 🔴 DINHEIRO. `decimal` no CLR + precisão DECLARADA (10,2), espelhando
        // VL_COBRADO NUMBER(10,2) da V18. Mesmo argumento de VL_PRECO em
        // ServicoPrecoConfiguration: InMemory fica verde com `double`, com precisão
        // errada ou sem HasPrecision nenhum, e o modo de falha real (medido na FD-07,
        // lado Java) é 999.99 → 1000 EM SILÊNCIO, não exceção.
        //
        // Aqui o dano é maior que em VL_PRECO: esta é a coluna que a FD-11 SOMA para
        // produzir receita bruta e ticket médio. Erro de centavo por linha vira erro
        // de relatório agregado.
        builder.Property(e => e.VlCobrado)
            .HasColumnName("VL_COBRADO")
            .HasPrecision(10, 2)
            .HasColumnType("NUMBER(10,2)")
            .IsRequired();

        // Nullable e sem CHECK na V18 — não é status de processamento (D-1).
        builder.Property(e => e.DsFormaPagamento)
            .HasColumnName("DS_FORMA_PAGAMENTO")
            .HasMaxLength(30)
            .HasColumnType("VARCHAR2(30)");

        builder.Property(e => e.DtCobranca)
            .HasColumnName("DT_COBRANCA")
            .IsRequired();

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVA")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        // Espelha os índices da V18. Declarativo — quem os cria é o Flyway; existem
        // aqui para o modelo não divergir do banco sem que ninguém perceba.
        builder.HasIndex(e => new { e.IdClinica, e.DtCobranca })
            .HasDatabaseName("IDX_COBRANCA_CLINICA_DATA");

        builder.HasIndex(e => e.IdEventoClinico)
            .HasDatabaseName("IDX_COBRANCA_EVENTO");
    }
}
