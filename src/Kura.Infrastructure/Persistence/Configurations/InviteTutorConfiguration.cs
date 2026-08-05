namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class InviteTutorConfiguration : IEntityTypeConfiguration<InviteTutor>
{
    public void Configure(EntityTypeBuilder<InviteTutor> builder)
    {
        builder.ToTable("INVITE_TUTOR");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_INVITE")
            .HasDefaultValueSql("SEQ_INVITE_TUTOR.NEXTVAL");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR")
            .IsRequired();

        // TASK-33 (E-1): sem HasConversion, o EF grava o Guid no layout binário mixed-endian
        // nativo (RAW/hex) na coluna NR_TOKEN — que o Flyway (autoridade de DDL) criou como
        // VARCHAR2(36) e que o Java lê como string canônica hifenizada (InviteTutor.java:26-27,
        // OnboardingService.java:68-70). A conversão explícita alinha a escrita .NET ao formato
        // que o outro lado do sistema já espera.
        builder.Property(e => e.NrToken)
            .HasColumnName("NR_TOKEN")
            .HasMaxLength(36)
            .HasConversion(g => g.ToString(), s => Guid.Parse(s))
            .IsRequired();

        builder.Property(e => e.DtExpiracao)
            .HasColumnName("DT_EXPIRACAO")
            .IsRequired();

        builder.Property(e => e.DsCanal)
            .HasColumnName("DS_CANAL")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.StUtilizado)
            .HasColumnName("ST_UTILIZADO")
            .IsRequired();

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVO")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasOne(e => e.Tutor)
            .WithMany(t => t.Invites)
            .HasForeignKey(e => e.IdTutor)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => e.StAtiva);
    }
}
