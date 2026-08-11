namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class InteracaoCanalConfiguration : IEntityTypeConfiguration<InteracaoCanal>
{
    public void Configure(EntityTypeBuilder<InteracaoCanal> builder)
    {
        builder.ToTable("INTERACAO_CANAL");

        builder.HasKey(e => e.Id);

        // .NET-owned: PK por sequence, não IDENTITY (ver V12-pk-strategy-map.md e o
        // comentário de cabeçalho de V15__interacao_canal.sql, backend-tutor-java).
        builder.Property(e => e.Id)
            .HasColumnName("ID_INTERACAO")
            .HasDefaultValueSql("SEQ_INTERACAO_CANAL.NEXTVAL");

        // Nullable desde a TASK-77 (FIX_7): interação de tutor não identificado grava
        // com ID_CLINICA null (decisão de produto, ver LunaService.RegistrarInteracaoAsync
        // e InteracaoCanal.cs). Coluna Oracle já nullable desde
        // V16__interacao_canal_clinica_nullable.sql (backend-tutor-java, TASK-76) — sem
        // IsRequired() aqui para o EF não inferir NOT NULL a partir do tipo `long?`.
        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR");

        // TASK-67 nota de fidelidade EF↔Flyway: sem HasColumnType explícito, o
        // provider Oracle.EntityFrameworkCore mapeia `string` para NVARCHAR2, mas
        // V15__interacao_canal.sql (Flyway, quem realmente cria a tabela) usa
        // VARCHAR2 puro em todas as colunas de texto. Essa divergência NVARCHAR2 vs
        // VARCHAR2 já é sistêmica em todo o restante deste repo (ex.:
        // AddConsultaTable.cs gera "NVARCHAR2(2000)" pra colunas que a V9 do Flyway
        // criou como VARCHAR2) e não é escopo desta task corrigir globalmente — mas
        // como esta tabela é inteiramente nova, HasColumnType explícito aqui evita
        // introduzir a MESMA divergência de novo, e sobretudo evita um problema mais
        // sério: sem o override, DS_CONTEUDO (maxLength 4000) faz o provider Oracle
        // trocar o tipo pra NCLOB inteiro (NVARCHAR2 tem teto de 4000 BYTES, que em
        // AL16UTF16 é 2000 caracteres) — um LOB é um tipo fundamentalmente diferente
        // de VARCHAR2(4000) (a coluna real, criada pela V15). Ver TASK-67 relatório,
        // seção "Migrations EF" para o antes/depois desta correção.
        builder.Property(e => e.DsCanal)
            .HasColumnName("DS_CANAL")
            .HasMaxLength(20)
            .HasColumnType("VARCHAR2(20)")
            .IsRequired();

        builder.Property(e => e.DsDirecao)
            .HasColumnName("DS_DIRECAO")
            .HasMaxLength(20)
            .HasColumnType("VARCHAR2(20)")
            .IsRequired();

        builder.Property(e => e.DsConteudo)
            .HasColumnName("DS_CONTEUDO")
            .HasMaxLength(4000)
            .HasColumnType("VARCHAR2(4000)")
            .IsRequired();

        builder.Property(e => e.DtRecebimento)
            .HasColumnName("DT_RECEBIMENTO")
            .IsRequired();

        builder.Property(e => e.DsMetadados)
            .HasColumnName("DS_METADADOS")
            .HasColumnType("CLOB")
            .IsRequired(false);

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
