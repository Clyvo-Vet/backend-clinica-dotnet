using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kura.Infrastructure.Persistence.Configurations;

public class ClinicaConfiguration : IEntityTypeConfiguration<Clinica>
{
    public void Configure(EntityTypeBuilder<Clinica> builder)
    {
        builder.ToTable("CLINICA");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_CLINICA")
            .HasDefaultValueSql("SEQ_CLINICA.NEXTVAL");

        builder.Property(e => e.NmClinica).HasColumnName("NM_CLINICA").HasMaxLength(120).IsRequired();
        builder.Property(e => e.NrCnpj).HasColumnName("NR_CNPJ").HasMaxLength(18).IsRequired();
        builder.Property(e => e.NmRazaoSocial).HasColumnName("NM_RAZAO_SOCIAL").HasMaxLength(150);
        builder.Property(e => e.DsEndereco).HasColumnName("DS_ENDERECO").HasMaxLength(200).IsRequired();
        builder.Property(e => e.NmCidade).HasColumnName("NM_CIDADE").HasMaxLength(80).IsRequired();
        builder.Property(e => e.SgUf).HasColumnName("SG_UF").HasMaxLength(2).IsRequired();
        builder.Property(e => e.NrCep).HasColumnName("NR_CEP").HasMaxLength(9).IsRequired();
        builder.Property(e => e.NrTelefone).HasColumnName("DS_TELEFONE").HasMaxLength(20);
        builder.Property(e => e.DsEmail).HasColumnName("DS_EMAIL").HasMaxLength(120).IsRequired();
        builder.Property(e => e.DtCadastro).HasColumnName("DT_CADASTRO");
        builder.Property(e => e.StAtiva).HasColumnName("ST_ATIVA");

        // EntidadeBase fields DtCriacao/DtAtualizacao are not columns in CLINICA table
        // (CLINICA uses DT_CADASTRO instead — mapped above via entity property DtCadastro)
        builder.Ignore(e => e.DtCriacao);
        builder.Ignore(e => e.DtAtualizacao);

        // Colunas adicionadas em V4 — autenticação da clínica
        builder.Property(e => e.DsEmailAcesso).HasColumnName("DS_EMAIL_ACESSO").HasMaxLength(120).IsRequired();
        builder.Property(e => e.DsSenhaHash).HasColumnName("DS_SENHA_HASH").HasMaxLength(256).IsRequired();

        builder.HasIndex(e => e.NrCnpj).IsUnique();
        builder.HasIndex(e => e.DsEmailAcesso).IsUnique();
        builder.HasQueryFilter(e => e.StAtiva);
    }
}
