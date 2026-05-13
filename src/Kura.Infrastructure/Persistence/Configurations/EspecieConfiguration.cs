namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EspecieConfiguration : IEntityTypeConfiguration<Especie>
{
    public void Configure(EntityTypeBuilder<Especie> builder)
    {
        builder.ToTable("ESPECIE");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_ESPECIE")
            .HasDefaultValueSql("SEQ_ESPECIE.NEXTVAL");

        builder.Property(e => e.NmEspecie)
            .HasColumnName("NM_ESPECIE")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        // ESPECIE table has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);
    }
}
