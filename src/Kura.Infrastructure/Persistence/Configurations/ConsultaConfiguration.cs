namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ConsultaConfiguration : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> builder)
    {
        builder.ToTable("CONSULTA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_CONSULTA")
            .HasDefaultValueSql("SEQ_CONSULTA.NEXTVAL");

        builder.Property(e => e.IdEventoClinico)
            .HasColumnName("ID_EVENTO_CLINICO")
            .IsRequired();

        builder.Property(e => e.DsMotivo)
            .HasColumnName("DS_MOTIVO")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DsAnamnese)
            .HasColumnName("DS_ANAMNESE")
            .HasMaxLength(2000);

        builder.Property(e => e.DsExameFisico)
            .HasColumnName("DS_EXAME_FISICO")
            .HasMaxLength(2000);

        builder.Property(e => e.DsDiagnostico)
            .HasColumnName("DS_DIAGNOSTICO")
            .HasMaxLength(1000);

        builder.Property(e => e.DtConsulta)
            .HasColumnName("DT_CONSULTA")
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

        builder.HasIndex(e => e.IdEventoClinico)
            .IsUnique();

        builder.HasOne(e => e.EventoClinico)
            .WithMany()
            .HasForeignKey(e => e.IdEventoClinico)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
