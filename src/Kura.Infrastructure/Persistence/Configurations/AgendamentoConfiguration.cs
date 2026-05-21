namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AgendamentoConfiguration : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("AGENDAMENTO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_AGENDAMENTO");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR");

        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO");

        builder.Property(e => e.NmPaciente)
            .HasColumnName("NM_PACIENTE")
            .HasMaxLength(200);

        builder.Property(e => e.DtAgendamento)
            .HasColumnName("DT_AGENDAMENTO")
            .IsRequired();

        builder.Property(e => e.NrDuracaoMinutos)
            .HasColumnName("NR_DURACAO_MINUTOS");

        builder.Property(e => e.DsServico)
            .HasColumnName("DS_SERVICO")
            .HasMaxLength(200);

        builder.Property(e => e.DsTipoConsulta)
            .HasColumnName("DS_TIPO")
            .HasMaxLength(30);

        builder.Property(e => e.StStatus)
            .HasColumnName("ST_STATUS")
            .HasMaxLength(50);

        builder.Property(e => e.DsOrigem)
            .HasColumnName("DS_ORIGEM")
            .HasMaxLength(100);

        builder.Property(e => e.NrVersion)
            .HasColumnName("NR_VERSION")
            .IsConcurrencyToken();

        // Flyway V5 columns
        builder.Property(e => e.DsObservacoes)
            .HasColumnName("DS_OBSERVACOES")
            .HasMaxLength(1000);

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO");

        builder.Property(e => e.DtConfirmacao)
            .HasColumnName("DT_CONFIRMACAO");

        builder.Property(e => e.DtCancelamento)
            .HasColumnName("DT_CANCELAMENTO");

        builder.Property(e => e.DsMotivoCancel)
            .HasColumnName("DS_MOTIVO_CANCEL")
            .HasMaxLength(500);

        builder.Property(e => e.IdEventoGerado)
            .HasColumnName("ID_EVENTO_GERADO");

        // AGENDAMENTO table (Java domain) has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.IdPet);

        builder.HasOne(e => e.Tutor)
            .WithMany()
            .HasForeignKey(e => e.IdTutor);

        builder.HasOne(e => e.Veterinario)
            .WithMany()
            .HasForeignKey(e => e.IdVeterinario);
    }
}
