namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("NOTIFICACAO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.Id)
            .HasColumnName("ID_NOTIFICACAO")
            .HasDefaultValueSql("SEQ_NOTIFICACAO.NEXTVAL");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR");

        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO");

        builder.Property(e => e.DsTitulo)
            .HasColumnName("DS_TITULO")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DsMensagem)
            .HasColumnName("DS_MENSAGEM")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.StLida)
            .HasColumnName("ST_LIDA")
            .IsRequired();

        builder.Property(e => e.DtLeitura)
            .HasColumnName("DT_LEITURA");

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasOne(e => e.Tutor)
            .WithMany()
            .HasForeignKey(e => e.IdTutor)
            .OnDelete(DeleteBehavior.Restrict);

        // NOTIFICACAO table has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.Veterinario)
            .WithMany()
            .HasForeignKey(e => e.IdVeterinario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
