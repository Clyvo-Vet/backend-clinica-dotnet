namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EventoClinicoConfiguration : IEntityTypeConfiguration<EventoClinico>
{
    public void Configure(EntityTypeBuilder<EventoClinico> builder)
    {
        builder.ToTable("EVENTO_CLINICO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.Id)
            .HasColumnName("ID_EVENTO")
            .HasDefaultValueSql("SEQ_EVENTO_CLINICO.NEXTVAL");

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET")
            .IsRequired();

        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO")
            .IsRequired();

        builder.Property(e => e.IdTipoEvento)
            .HasColumnName("ID_TIPO_EVENTO")
            .IsRequired();

        builder.Property(e => e.DtEvento)
            .HasColumnName("DT_EVENTO")
            .IsRequired();

        builder.Property(e => e.DsObservacao)
            .HasColumnName("DS_OBSERVACAO")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.DsTranscricao)
            .HasColumnName("DS_TRANSCRICAO")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.DsSoapS)
            .HasColumnName("DS_SOAP_S")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.DsSoapO)
            .HasColumnName("DS_SOAP_O")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.DsSoapA)
            .HasColumnName("DS_SOAP_A")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.DsSoapP)
            .HasColumnName("DS_SOAP_P")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.StSoapConfirmado)
            .HasColumnName("ST_SOAP_CONFIRMADO")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.IdPet)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Veterinario)
            .WithMany()
            .HasForeignKey(e => e.IdVeterinario)
            .OnDelete(DeleteBehavior.Restrict);

        // EVENTO_CLINICO table has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.TipoEvento)
            .WithMany()
            .HasForeignKey(e => e.IdTipoEvento)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
