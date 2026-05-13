namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ExameConfiguration : IEntityTypeConfiguration<Exame>
{
    public void Configure(EntityTypeBuilder<Exame> builder)
    {
        builder.ToTable("EXAME");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_EXAME")
            .HasDefaultValueSql("SEQ_EXAME.NEXTVAL");

        builder.Property(e => e.IdEventoClinico)
            .HasColumnName("ID_EVENTO_CLINICO")
            .IsRequired();

        builder.Property(e => e.NmExame)
            .HasColumnName("NM_EXAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DsResultado)
            .HasColumnName("DS_RESULTADO")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.DtRealizacao)
            .HasColumnName("DT_REALIZACAO")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        // EXAME table has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.EventoClinico)
            .WithMany()
            .HasForeignKey(e => e.IdEventoClinico)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
