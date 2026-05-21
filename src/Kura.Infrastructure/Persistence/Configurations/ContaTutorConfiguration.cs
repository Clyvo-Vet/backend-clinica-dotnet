namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ContaTutorConfiguration : IEntityTypeConfiguration<ContaTutor>
{
    public void Configure(EntityTypeBuilder<ContaTutor> builder)
    {
        builder.ToTable("CONTA_TUTOR", tb => tb.ExcludeFromMigrations());

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_CONTA");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR")
            .IsRequired();

        builder.Property(e => e.DsEmail)
            .HasColumnName("DS_EMAIL_LOGIN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.StEmailVerificado)
            .HasColumnName("ST_EMAIL_VERIFICADO")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCadastro)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.HasOne(e => e.Tutor)
            .WithMany()
            .HasForeignKey(e => e.IdTutor);
    }
}
