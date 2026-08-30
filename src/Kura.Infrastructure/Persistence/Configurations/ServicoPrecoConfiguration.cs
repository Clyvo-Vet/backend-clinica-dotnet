namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Mapeamento de <see cref="ServicoPreco"/> (FD-08, ciclo FIN).
///
/// <para>Fonte da verdade do schema: <c>V18__financeiro.sql</c> no repo
/// <c>backend-tutor-java</c> (Flyway é a única autoridade de DDL — ver
/// <c>MIGRATIONS_POLICY.md</c>; o EF nunca executa DDL contra o Oracle). Cada
/// dimensão abaixo foi copiada daquele <c>CREATE TABLE</c>, não inferida.</para>
///
/// <para>⚠️ <b>Nenhum <c>HasQueryFilter</c> aqui, de propósito.</b> O filtro desta
/// entidade vive num lugar só: <c>KuraDbContext.ApplyTenantFilters</c>. Medido na
/// revisão G2 da FD-02 (EF Core 10.0.7): dois filtros <b>anônimos</b> para a mesma
/// entidade não se combinam nem lançam — o segundo <b>SUBSTITUI</b> o primeiro em
/// silêncio; anônimo + nomeado lança; só dois nomeados combinam com AND. Como
/// <c>ApplyConfigurationsFromAssembly</c> roda ANTES de <c>ApplyTenantFilters</c>, um
/// filtro anônimo declarado aqui seria apagado por aquele: código morto e enganoso.
/// A guarda é
/// <c>ServicoPrecoTenantIsolationTests.Configuracao_NaoDeclaraQueryFilterProprio</c>,
/// que monta um modelo só com esta configuração — nenhum teste sobre o modelo
/// completo consegue enxergar um filtro que já foi substituído.</para>
/// </summary>
public class ServicoPrecoConfiguration : IEntityTypeConfiguration<ServicoPreco>
{
    public void Configure(EntityTypeBuilder<ServicoPreco> builder)
    {
        builder.ToTable("SERVICO_PRECO");

        builder.HasKey(e => e.Id);

        // .NET-owned ⇒ PK por sequence, não IDENTITY (docs/V12-pk-strategy-map.md; a
        // V18 declara DEFAULT SEQ_SERVICO_PRECO.NEXTVAL). HasColumnType("NUMBER(10)")
        // explícito porque, sem override, Oracle.EntityFrameworkCore mapeia `long`
        // para NUMBER(19) e a V18 declara NUMBER(10) — cosmético (o EF não cria a
        // coluna), mas evita introduzir divergência EF↔Flyway numa tabela nova.
        builder.Property(e => e.Id)
            .HasColumnName("ID_SERVICO_PRECO")
            .HasColumnType("NUMBER(10)")
            .HasDefaultValueSql("SEQ_SERVICO_PRECO.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .HasColumnType("NUMBER(10)")
            .IsRequired();

        // VARCHAR2 explícito (não NVARCHAR2, default do provider Oracle) — a V18 usa
        // VARCHAR2 puro em todas as colunas de texto.
        builder.Property(e => e.NmServico)
            .HasColumnName("NM_SERVICO")
            .HasMaxLength(200)
            .HasColumnType("VARCHAR2(200)")
            .IsRequired();

        // 🔴 DINHEIRO. `decimal` no CLR + precisão DECLARADA (10,2), espelhando
        // VL_PRECO NUMBER(10,2) da V18.
        //
        // Por que HasPrecision explícito, e não só o tipo CLR: sem ele o provider
        // Oracle escolhe a precisão por default do mapeamento, e o modelo passa a
        // afirmar uma coisa diferente do banco. Este é o PRIMEIRO HasPrecision deste
        // repositório (medido: 0 ocorrências em Configurations/ antes da FD-08) —
        // as outras colunas decimais existentes (LeituraTemperatura, AlertaTemperatura)
        // não declaram precisão, e não são dinheiro.
        //
        // ⚠️ O provider InMemory da suíte NÃO reprova nada disto: a suíte fica verde
        // com `double`, com precisão errada ou sem HasPrecision nenhum. Quem reprova é
        // a FD-12 (Oracle real) — e, até lá, o único detector é
        // ServicoPrecoTenantIsolationTests.Mapeamento_TravaTipoEPrecisaoDoDinheiro.
        //
        // O modo de falha, medido na FD-07 do lado Java: NUMBER(10) no lugar de
        // NUMBER(10,2) faz 999.99 virar 1000 EM SILÊNCIO — arredondamento, não exceção.
        builder.Property(e => e.VlPreco)
            .HasColumnName("VL_PRECO")
            .HasPrecision(10, 2)
            .HasColumnType("NUMBER(10,2)")
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
    }
}
