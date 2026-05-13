namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DispositivoIotConfiguration : IEntityTypeConfiguration<DispositivoIot>
{
    public void Configure(EntityTypeBuilder<DispositivoIot> builder)
    {
        builder.ToTable("DISPOSITIVO_IOT");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_DISPOSITIVO")
            .HasDefaultValueSql("SEQ_DISPOSITIVO_IOT.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.CdDispositivo)
            .HasColumnName("CD_DISPOSITIVO")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.DsDescricao)
            .HasColumnName("DS_DESCRICAO")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DsLocalizacao)
            .HasColumnName("DS_LOCALIZACAO")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasIndex(e => e.CdDispositivo).IsUnique();

        // DISPOSITIVO_IOT table has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.Clinica)
            .WithMany()
            .HasForeignKey(e => e.IdClinica)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
