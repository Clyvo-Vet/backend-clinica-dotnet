namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Mapeamento de <see cref="UsuarioClinica"/> (FD-02, ciclo FIN).
///
/// <para>Fonte da verdade do schema: <c>V17__usuario_clinica.sql</c> no repo
/// <c>backend-tutor-java</c> (Flyway é a única autoridade de DDL — ver
/// <c>MIGRATIONS_POLICY.md</c>; o EF nunca executa DDL contra o Oracle). Cada
/// dimensão abaixo foi copiada daquele <c>CREATE TABLE</c>, não inferida.</para>
///
/// <para>⚠️ <b>Nenhum <c>HasQueryFilter</c> aqui, de propósito.</b> Duas chamadas
/// <c>HasQueryFilter()</c> para a mesma entidade NÃO se combinam com AND no EF
/// Core 10 — a última registrada SUBSTITUI a anterior inteira. Foi assim que
/// <c>Tutor</c> perdeu o isolamento de tenant até a TASK-21. O filtro desta
/// entidade vive num lugar só: <c>KuraDbContext.ApplyTenantFilters</c>.</para>
/// </summary>
public class UsuarioClinicaConfiguration : IEntityTypeConfiguration<UsuarioClinica>
{
    public void Configure(EntityTypeBuilder<UsuarioClinica> builder)
    {
        builder.ToTable("USUARIO_CLINICA");

        builder.HasKey(e => e.Id);

        // .NET-owned ⇒ PK por sequence, não IDENTITY (V12/docs/V12-pk-strategy-map.md,
        // e o cabeçalho da própria V17, que registra o mesmo argumento). IDENTITY é o
        // padrão Java-owned; usá-lo aqui repetiria o erro da V9 (ORA-02289).
        //
        // HasColumnType("NUMBER(10)") explícito pelo mesmo motivo da TASK-86 em
        // InteracaoCanalConfiguration: sem override, Oracle.EntityFrameworkCore mapeia
        // `long` para NUMBER(19), mas a V17 declara NUMBER(10). É cosmético (o EF não
        // cria a coluna), mas evita introduzir divergência EF↔Flyway numa tabela nova.
        builder.Property(e => e.Id)
            .HasColumnName("ID_USUARIO_CLINICA")
            .HasColumnType("NUMBER(10)")
            .HasDefaultValueSql("SEQ_USUARIO_CLINICA.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .HasColumnType("NUMBER(10)")
            .IsRequired();

        // NULLABLE na V17 (gestor que não é veterinário). Sem IsRequired() para o EF
        // não inferir NOT NULL — o tipo CLR já é `long?`, mas a ausência é deliberada
        // e documentada, não esquecimento.
        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO")
            .HasColumnType("NUMBER(10)");

        // VARCHAR2 explícito (não NVARCHAR2, que é o default do provider Oracle) —
        // a V17 usa VARCHAR2 puro em todas as colunas de texto.
        builder.Property(e => e.DsEmail)
            .HasColumnName("DS_EMAIL")
            .HasMaxLength(120)
            .HasColumnType("VARCHAR2(120)")
            .IsRequired();

        builder.Property(e => e.DsSenhaHash)
            .HasColumnName("DS_SENHA_HASH")
            .HasMaxLength(256)
            .HasColumnType("VARCHAR2(256)")
            .IsRequired();

        builder.Property(e => e.TpPerfil)
            .HasColumnName("TP_PERFIL")
            .HasMaxLength(20)
            .HasColumnType("VARCHAR2(20)")
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

        // Espelha UK_USUARIO_CLINICA_EMAIL da V17: unicidade POR CLÍNICA, não global
        // (a mesma pessoa pode atender em duas clínicas). Declarativo — quem impõe é o
        // Oracle; o provider InMemory da suíte não valida índice único.
        builder.HasIndex(e => new { e.IdClinica, e.DsEmail })
            .IsUnique();
    }
}
